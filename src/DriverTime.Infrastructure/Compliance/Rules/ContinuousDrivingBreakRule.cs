using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ContinuousDrivingBreakRule : IComplianceRule
{
    private const string RuleCode = "CONTINUOUS_DRIVING_BREAK";
    private const long LimitMinutes = 270;
    private const long RequiredBreakMinutes = 45;
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromMinutes(LimitMinutes);
    private static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(RequiredBreakMinutes);

    public string Name => "Continuous driving break";

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var continuousDriving = TimeSpan.Zero;
        DateTime? continuousStartUtc = null;

        foreach (var activity in timeline.OrderBy(x => x.StartUtc))
        {
            if (IsDriving(activity))
            {
                continuousStartUtc ??= activity.StartUtc;
                continuousDriving += activity.Duration;

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
                        Metadata = new Dictionary<string, long>
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
                continuousDriving = TimeSpan.Zero;
                continuousStartUtc = null;
            }
        }

        return result;
    }

    private static bool IsDriving(TimelineActivity activity) =>
        activity.ActivityType.Equals("DRIVING", StringComparison.OrdinalIgnoreCase);

    private static bool IsResettingBreak(TimelineActivity activity) =>
        (activity.ActivityType.Equals("REST", StringComparison.OrdinalIgnoreCase) ||
         activity.ActivityType.Equals("AVAILABILITY", StringComparison.OrdinalIgnoreCase)) &&
        activity.Duration >= RequiredBreak;

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
