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
        var violationPendingAfterSplitStart = false;
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
                    if (pendingFirstSplitBreak >= FirstSplitBreak)
                    {
                        violationPendingAfterSplitStart = true;
                        continue;
                    }

                    AddViolation(
                        result,
                        continuousStartUtc.Value,
                        activity.EndUtc,
                        continuousDriving,
                        pendingFirstSplitBreak,
                        "Brak wymaganej przerwy");

                    ResetDrivingPeriod(
                        ref continuousDriving,
                        ref continuousStartUtc,
                        ref pendingFirstSplitBreak,
                        ref violationPendingAfterSplitStart);
                }

                continue;
            }

            if (!IsQualifyingBreakActivity(activity))
            {
                continue;
            }

            if (activity.Duration >= RequiredBreak)
            {
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak,
                    ref violationPendingAfterSplitStart);
                continue;
            }

            if (pendingFirstSplitBreak >= FirstSplitBreak && activity.Duration >= SecondSplitBreak)
            {
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak,
                    ref violationPendingAfterSplitStart);
                continue;
            }

            if (pendingFirstSplitBreak < FirstSplitBreak && activity.Duration >= FirstSplitBreak)
            {
                pendingFirstSplitBreak = activity.Duration;
            }
        }

        if (violationPendingAfterSplitStart && continuousStartUtc.HasValue)
        {
            AddViolation(
                result,
                continuousStartUtc.Value,
                normalizedTimeline.Last(x => x.StartUtc < x.EndUtc).EndUtc,
                continuousDriving,
                pendingFirstSplitBreak,
                "Brak drugiej części przerwy dzielonej minimum 30 minut");
        }
        else if (continuousDriving >= ContinuousDrivingLimit &&
            pendingFirstSplitBreak >= FirstSplitBreak &&
            continuousStartUtc.HasValue)
        {
            AddViolation(
                result,
                continuousStartUtc.Value,
                normalizedTimeline.Last(x => x.StartUtc < x.EndUtc).EndUtc,
                continuousDriving,
                pendingFirstSplitBreak,
                "Nieprawidłowa przerwa dzielona");
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
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static bool IsQualifyingBreakActivity(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase) ||
        activity.ActivityType.Equals(ActivityTypeNormalizer.Availability, StringComparison.OrdinalIgnoreCase);

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
            Description = $"Ciągła jazda trwała {FormatDuration(continuousDriving)} bez wymaganej przerwy 45 minut albo prawidłowej przerwy dzielonej 15 + 30 minut.",
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
        ref TimeSpan pendingFirstSplitBreak,
        ref bool violationPendingAfterSplitStart)
    {
        continuousDriving = TimeSpan.Zero;
        continuousStartUtc = null;
        pendingFirstSplitBreak = TimeSpan.Zero;
        violationPendingAfterSplitStart = false;
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
