using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

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

    private readonly ILogger<DailyRestViolationRule> _logger;

    public DailyRestViolationRule(ILogger<DailyRestViolationRule> logger)
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

        var ordered = timeline
            .OrderBy(x => x.StartUtc)
            .ToList();
        var workDays = 0;
        var daysBelowRegularRest = 0;
        var daysBelowReducedRest = 0;
        var maxLongestRest = TimeSpan.Zero;

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

            workDays++;

            var longestRest = dayActivities
                .Where(IsRest)
                .Select(x => x.Duration)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();
            var restMinutes = (long)Math.Round(longestRest.TotalMinutes);
            maxLongestRest = Max(maxLongestRest, longestRest);

            if (longestRest >= RegularDailyRest)
            {
                continue;
            }

            daysBelowRegularRest++;

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

            daysBelowReducedRest++;

            result.Violations.Add(CreateViolation(
                severity: "HIGH",
                description: "Brak prawidłowego odpoczynku dziennego minimum 9 godzin między okresami pracy lub jazdy.",
                startUtc: workActivities[0].StartUtc,
                endUtc: workActivities[^1].EndUtc,
                restMinutes: restMinutes));
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: workDays={WorkDays}, maxLongestRestMinutes={MaxLongestRestMinutes}, daysBelow11h={DaysBelowRegularRest}, daysBelow9h={DaysBelowReducedRest}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            workDays,
            (long)Math.Round(maxLongestRest.TotalMinutes),
            daysBelowRegularRest,
            daysBelowReducedRest,
            result.Violations.Count);

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

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
