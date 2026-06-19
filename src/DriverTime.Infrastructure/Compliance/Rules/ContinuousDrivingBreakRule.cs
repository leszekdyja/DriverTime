using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ContinuousDrivingBreakRule : IComplianceRule
{
    private const string RuleCode = "CONTINUOUS_DRIVING_BREAK";
    private const long LimitMinutes = 270;
    private const long RequiredBreakMinutes = 45;
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromMinutes(LimitMinutes);
    private static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(RequiredBreakMinutes);

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
                    result.Violations.Add(new ComplianceViolationCandidate
                    {
                        Code = RuleCode,
                        RuleName = Name,
                        Severity = "HIGH",
                        Description = $"Ciągła jazda trwała {FormatDuration(continuousDriving)} bez wymaganej przerwy co najmniej 45 minut.",
                        PeriodStartUtc = continuousStartUtc.Value,
                        PeriodEndUtc = activity.EndUtc,
                        ActualMinutes = (long)Math.Round(continuousDriving.TotalMinutes),
                        LimitMinutes = LimitMinutes,
                        Metadata = new Dictionary<string, object>
                        {
                            ["continuousDrivingMinutes"] = (long)Math.Round(continuousDriving.TotalMinutes),
                            ["limitMinutes"] = LimitMinutes,
                            ["requiredBreakMinutes"] = RequiredBreakMinutes
                        }
                    });

                    continuousDriving = TimeSpan.Zero;
                    continuousStartUtc = null;
                }

                continue;
            }

            if (IsResettingBreak(activity))
            {
                resettingBreaks++;
                continuousDriving = TimeSpan.Zero;
                continuousStartUtc = null;
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
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static bool IsResettingBreak(TimelineActivity activity) =>
        (activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase) ||
         activity.ActivityType.Equals(ActivityTypeNormalizer.Availability, StringComparison.OrdinalIgnoreCase)) &&
        activity.Duration >= RequiredBreak;

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
