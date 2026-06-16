using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class BiWeeklyDrivingLimitRule : IComplianceRule
{
    private static readonly TimeSpan BiWeeklyLimit = TimeSpan.FromHours(90);

    public string Name => "Bi-weekly driving limit";

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var weeks = timeline
            .Where(IsDriving)
            .GroupBy(x => GetIsoWeekStart(x.StartUtc))
            .Select(group => new
            {
                WeekStart = group.Key,
                Duration = group.Aggregate(TimeSpan.Zero, (sum, activity) => sum + activity.Duration)
            })
            .OrderBy(x => x.WeekStart)
            .ToList();

        for (var index = 1; index < weeks.Count; index++)
        {
            var previous = weeks[index - 1];
            var current = weeks[index];

            if (current.WeekStart != previous.WeekStart.AddDays(7))
            {
                continue;
            }

            var duration = previous.Duration + current.Duration;

            if (duration <= BiWeeklyLimit)
            {
                continue;
            }

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = "EU561_BIWEEKLY_DRIVING_90H",
                RuleName = Name,
                Severity = "Critical",
                Description = $"Czas jazdy w dwóch kolejnych tygodniach wyniósł {FormatDuration(duration)} i przekroczył limit 90 godzin.",
                PeriodStartUtc = previous.WeekStart,
                PeriodEndUtc = current.WeekStart.AddDays(7),
                ActualMinutes = (long)Math.Round(duration.TotalMinutes),
                LimitMinutes = (long)BiWeeklyLimit.TotalMinutes
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
