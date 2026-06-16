using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class DailyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "DAILY_DRIVING_LIMIT";
    private static readonly TimeSpan StandardDailyLimit = TimeSpan.FromHours(9);
    private static readonly TimeSpan ExtendedDailyLimit = TimeSpan.FromHours(10);

    public string Code => RuleCode;

    public string Name => "Daily driving limit";

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var dailyDriving = timeline
            .Where(IsDriving)
            .GroupBy(x => x.StartUtc.Date)
            .OrderBy(x => x.Key);

        foreach (var day in dailyDriving)
        {
            var activities = day
                .OrderBy(x => x.StartUtc)
                .ToList();
            var totalDriving = activities.Aggregate(
                TimeSpan.Zero,
                (sum, activity) => sum + activity.Duration);

            if (totalDriving <= StandardDailyLimit)
            {
                continue;
            }

            var severity = totalDriving > ExtendedDailyLimit
                ? "HIGH"
                : "MEDIUM";
            var limit = totalDriving > ExtendedDailyLimit
                ? ExtendedDailyLimit
                : StandardDailyLimit;
            var exceededMinutes = Math.Max(
                0,
                (long)Math.Round((totalDriving - StandardDailyLimit).TotalMinutes));

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = severity,
                Description = BuildMessage(totalDriving, severity),
                PeriodStartUtc = activities[0].StartUtc,
                PeriodEndUtc = activities[^1].EndUtc,
                ActualMinutes = (long)Math.Round(totalDriving.TotalMinutes),
                LimitMinutes = (long)limit.TotalMinutes,
                Metadata = new Dictionary<string, long>
                {
                    ["totalDrivingMinutes"] = (long)Math.Round(totalDriving.TotalMinutes),
                    ["exceededMinutes"] = exceededMinutes
                }
            });
        }

        return result;
    }

    private static bool IsDriving(TimelineActivity activity) =>
        activity.ActivityType.Equals("DRIVING", StringComparison.OrdinalIgnoreCase);

    private static string BuildMessage(TimeSpan totalDriving, string severity)
    {
        var formattedDuration = FormatDuration(totalDriving);

        return severity == "HIGH"
            ? $"Dzienny czas jazdy wyniósł {formattedDuration} i przekroczył limit 10 godzin."
            : $"Dzienny czas jazdy wyniósł {formattedDuration} i przekroczył standardowy limit 9 godzin.";
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
