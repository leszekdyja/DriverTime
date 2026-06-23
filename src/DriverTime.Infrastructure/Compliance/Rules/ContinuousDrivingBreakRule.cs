using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ContinuousDrivingBreakRule : IComplianceRule
{
    private const string RuleCode = "CONTINUOUS_DRIVING_BREAK";
    private const long LimitMinutes = 270;
    private const long RequiredBreakMinutes = 45;
    private const long FirstSplitBreakMinutes = 15;
    private const long SecondSplitBreakMinutes = 30;
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromMinutes(LimitMinutes);
    private static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(RequiredBreakMinutes);
    private static readonly TimeSpan FirstSplitBreak = TimeSpan.FromMinutes(FirstSplitBreakMinutes);
    private static readonly TimeSpan SecondSplitBreak = TimeSpan.FromMinutes(SecondSplitBreakMinutes);

    public string Code => RuleCode;

    public string Name => "Continuous driving break";

    private readonly ILogger<ContinuousDrivingBreakRule> _logger;

    public ContinuousDrivingBreakRule(ILogger<ContinuousDrivingBreakRule> logger)
    {
        _logger = logger;
    }

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var continuousDriving = TimeSpan.Zero;
        var maxContinuousDriving = TimeSpan.Zero;
        var drivingSegments = 0;
        var resettingBreaks = 0;
        var pendingFirstSplitBreak = TimeSpan.Zero;
        DateTime? continuousStartUtc = null;

        var normalizedTimeline = WeeklyDrivingTimelineHelper.GetMergedDrivingTimeline(timeline)
            .Concat(timeline.Where(activity => !IsDriving(activity)))
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        foreach (var activity in normalizedTimeline)
        {
            if (IsDriving(activity))
            {
                drivingSegments++;
                continuousStartUtc ??= activity.StartUtc;
                continuousDriving += activity.Duration;
                maxContinuousDriving = Max(maxContinuousDriving, continuousDriving);

                if (continuousDriving > ContinuousDrivingLimit)
                {
                    AddViolation(
                        result,
                        continuousStartUtc.Value,
                        activity.EndUtc,
                        continuousDriving,
                        pendingFirstSplitBreak,
                        pendingFirstSplitBreak >= FirstSplitBreak
                            ? "Missing second split break of at least 30 minutes"
                            : "Missing required break");

                    ResetDrivingPeriod(
                        ref continuousDriving,
                        ref continuousStartUtc,
                        ref pendingFirstSplitBreak);
                }

                continue;
            }

            if (!IsQualifyingBreakActivity(activity))
            {
                pendingFirstSplitBreak = TimeSpan.Zero;
                continue;
            }

            if (activity.Duration >= RequiredBreak)
            {
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak);
                continue;
            }

            if (pendingFirstSplitBreak >= FirstSplitBreak && activity.Duration >= SecondSplitBreak)
            {
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak);
                continue;
            }

            if (pendingFirstSplitBreak < FirstSplitBreak && activity.Duration >= FirstSplitBreak)
            {
                pendingFirstSplitBreak = activity.Duration;
            }
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: drivingSegments={DrivingSegments}, resettingBreaks={ResettingBreaks}, maxContinuousDrivingMinutes={MaxContinuousDrivingMinutes}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            drivingSegments,
            resettingBreaks,
            (long)Math.Round(maxContinuousDriving.TotalMinutes),
            result.Violations.Count);

        return result;
    }

    private static bool IsDriving(TimelineActivity activity) =>
        ActivityTypeNormalizer.Normalize(activity.ActivityType)
            .Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static bool IsQualifyingBreakActivity(TimelineActivity activity) =>
        ActivityTypeNormalizer.Normalize(activity.ActivityType) is ActivityTypeNormalizer.Rest or ActivityTypeNormalizer.Availability;

    private static void AddViolation(
        ComplianceRuleResult result,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        TimeSpan continuousDriving,
        TimeSpan receivedBreak,
        string breakType)
    {
        var continuousDrivingMinutes = (long)Math.Round(continuousDriving.TotalMinutes);
        var receivedBreakMinutes = (long)Math.Round(receivedBreak.TotalMinutes);
        var exceededMinutes = Math.Max(continuousDrivingMinutes - LimitMinutes, 0);

        result.Violations.Add(new ComplianceViolationCandidate
        {
            Code = RuleCode,
            RuleName = "Continuous driving break",
            Severity = "HIGH",
            Description = $"Continuous driving lasted {FormatDuration(continuousDriving)} without the required 45 minute break or valid 15 + 30 split break.",
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            ActualMinutes = continuousDrivingMinutes,
            LimitMinutes = LimitMinutes,
            Metadata = new Dictionary<string, object>
            {
                ["continuousDrivingMinutes"] = continuousDrivingMinutes,
                ["limitMinutes"] = LimitMinutes,
                ["requiredBreakMinutes"] = RequiredBreakMinutes,
                ["receivedBreakMinutes"] = receivedBreakMinutes,
                ["exceededMinutes"] = exceededMinutes,
                ["breakType"] = breakType,
                ["splitBreakFirstPartMinutes"] = FirstSplitBreakMinutes,
                ["splitBreakSecondPartMinutes"] = SecondSplitBreakMinutes
            }
        });
    }

    private static void ResetDrivingPeriod(
        ref TimeSpan continuousDriving,
        ref DateTime? continuousStartUtc,
        ref TimeSpan pendingFirstSplitBreak)
    {
        continuousDriving = TimeSpan.Zero;
        continuousStartUtc = null;
        pendingFirstSplitBreak = TimeSpan.Zero;
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
