using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class WeeklyDrivingLimitRule : IComplianceRule
{
    private static readonly TimeSpan WeeklyLimit = TimeSpan.FromHours(56);

    public string Code => "WEEKLY_DRIVING_LIMIT";

    public string Name => "Weekly driving limit";

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var weeklyDriving = timeline
            .Where(IsDriving)
            .GroupBy(x => GetIsoWeekStart(x.StartUtc))
            .Select(group => new
            {
                WeekStart = group.Key,
                Duration = group.Aggregate(TimeSpan.Zero, (sum, activity) => sum + activity.Duration)
            })
            .OrderBy(x => x.WeekStart);

        foreach (var week in weeklyDriving.Where(x => x.Duration > WeeklyLimit))
        {
            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = "EU561_WEEKLY_DRIVING_56H",
                RuleName = Name,
                Severity = "Critical",
                Description = $"Tygodniowy czas jazdy wyniósł {FormatDuration(week.Duration)} i przekroczył limit 56 godzin.",
                PeriodStartUtc = week.WeekStart,
                PeriodEndUtc = week.WeekStart.AddDays(7),
                ActualMinutes = (long)Math.Round(week.Duration.TotalMinutes),
                LimitMinutes = (long)WeeklyLimit.TotalMinutes
            });
        }

        return result;
    }

    private static bool IsDriving(TimelineActivity activity) =>
        activity.ActivityType.Equals("DRIVING", StringComparison.OrdinalIgnoreCase);

    private static DateTime GetIsoWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);

        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
