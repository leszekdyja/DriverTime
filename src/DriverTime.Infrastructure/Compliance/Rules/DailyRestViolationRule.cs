using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class DailyRestViolationRule : IComplianceRule
{
    private const string RuleCode = "DAILY_REST";
    private const long RegularDailyRestMinutes = 660;
    private const long ReducedDailyRestMinutes = 540;
    private static readonly TimeSpan RegularDailyRest = TimeSpan.FromMinutes(RegularDailyRestMinutes);
    private static readonly TimeSpan ReducedDailyRest = TimeSpan.FromMinutes(ReducedDailyRestMinutes);

    public string Code => RuleCode;

    public string Name => "Daily rest";

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var ordered = timeline
            .OrderBy(x => x.StartUtc)
            .ToList();

        foreach (var day in ordered
                     .Where(IsWorkPeriodActivity)
                     .GroupBy(x => x.StartUtc.Date)
                     .OrderBy(x => x.Key))
        {
            var dayStart = day.Key;
            var dayEnd = dayStart.AddDays(1);
            var dayActivities = ordered
                .Where(x => x.StartUtc < dayEnd && x.EndUtc > dayStart)
                .ToList();
            var workActivities = dayActivities
                .Where(IsWorkPeriodActivity)
                .OrderBy(x => x.StartUtc)
                .ToList();

            if (workActivities.Count == 0)
            {
                continue;
            }

            var longestRest = dayActivities
                .Where(IsRest)
                .Select(x => x.Duration)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
            var restMinutes = (long)Math.Round(longestRest.TotalMinutes);

            if (longestRest >= RegularDailyRest)
            {
                continue;
            }

            if (longestRest >= ReducedDailyRest)
            {
                result.Violations.Add(CreateViolation(
                    severity: "MEDIUM",
                    description: $"Wykryto skrócony odpoczynek dzienny: {FormatDuration(longestRest)} zamiast regularnych 11 godzin.",
                    startUtc: workActivities[0].StartUtc,
                    endUtc: workActivities[^1].EndUtc,
                    restMinutes: restMinutes));

                continue;
            }

            result.Violations.Add(CreateViolation(
                severity: "HIGH",
                description: "Brak prawidłowego odpoczynku dziennego minimum 9 godzin między okresami pracy lub jazdy.",
                startUtc: workActivities[0].StartUtc,
                endUtc: workActivities[^1].EndUtc,
                restMinutes: restMinutes));
        }

        return result;
    }

    private static ComplianceViolationCandidate CreateViolation(
        string severity,
        string description,
        DateTime startUtc,
        DateTime endUtc,
        long restMinutes)
    {
        return new ComplianceViolationCandidate
        {
            Code = RuleCode,
            RuleName = "Daily rest",
            Severity = severity,
            Description = description,
            PeriodStartUtc = startUtc,
            PeriodEndUtc = endUtc,
            ActualMinutes = restMinutes,
            LimitMinutes = RegularDailyRestMinutes,
            Metadata = new Dictionary<string, long>
            {
                ["restMinutes"] = restMinutes,
                ["regularDailyRestMinutes"] = RegularDailyRestMinutes,
                ["reducedDailyRestMinutes"] = ReducedDailyRestMinutes
            }
        };
    }

    private static bool IsRest(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkPeriodActivity(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase) ||
        activity.ActivityType.Equals(ActivityTypeNormalizer.Work, StringComparison.OrdinalIgnoreCase) ||
        activity.ActivityType.Equals(ActivityTypeNormalizer.Availability, StringComparison.OrdinalIgnoreCase);

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
