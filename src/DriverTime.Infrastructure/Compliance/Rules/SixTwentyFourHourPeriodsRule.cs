using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class SixTwentyFourHourPeriodsRule : IComplianceRule
{
    private const string RuleCode = "SIX_24H_PERIODS";

    private readonly ILogger<SixTwentyFourHourPeriodsRule> _logger;

    public SixTwentyFourHourPeriodsRule(ILogger<SixTwentyFourHourPeriodsRule> logger)
    {
        _logger = logger;
    }

    public string Code => RuleCode;

    public string Name => "Six 24-hour periods";

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var validTimeline = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var weeklyRests = WeeklyRestTimelineHelper.BuildWeeklyRestPeriods(validTimeline);

        if (weeklyRests.Count == 0)
        {
            LogResult(driverId, weeklyRests.Count, checkedPeriods: 0, result.Violations.Count);
            return result;
        }

        var sixPeriodViolations = WeeklyRestTimelineHelper.FindSixPeriodViolations(validTimeline);
        foreach (var sixPeriodViolation in sixPeriodViolations)
        {
            result.Violations.Add(CreateViolation(sixPeriodViolation));
        }

        LogResult(driverId, weeklyRests.Count, sixPeriodViolations.Count, result.Violations.Count);

        return result;
    }

    private void LogResult(
        Guid driverId,
        int weeklyRests,
        int checkedPeriods,
        int violationCount)
    {
        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeklyRests={WeeklyRests}, checkedPeriods={CheckedPeriods}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weeklyRests,
            checkedPeriods,
            violationCount);
    }

    private static ComplianceViolationCandidate CreateViolation(
        WeeklyRestTimelineHelper.SixPeriodViolation sixPeriodViolation)
    {
        var previousWeeklyRest = sixPeriodViolation.PreviousWeeklyRest;
        var nextWeeklyRestStartUtc = sixPeriodViolation.NextWeeklyRestStartUtc;
        var deadlineUtc = sixPeriodViolation.DeadlineUtc;
        var comparisonUtc = sixPeriodViolation.ComparisonUtc;
        var exceededMinutes = Math.Max(0, (long)Math.Ceiling((comparisonUtc - deadlineUtc).TotalMinutes));
        var actualMinutes = Math.Max(0, (long)Math.Ceiling((comparisonUtc - previousWeeklyRest.EndUtc).TotalMinutes));

        return new ComplianceViolationCandidate
        {
            Code = RuleCode,
            RuleName = "Six 24-hour periods",
            Severity = "High",
            Description = nextWeeklyRestStartUtc.HasValue
                ? $"Kolejny odpoczynek tygodniowy rozpoczął się po terminie sześciu okresów 24h. Przekroczenie: {FormatDuration(TimeSpan.FromMinutes(exceededMinutes))}."
                : "Nie znaleziono kolejnego odpoczynku tygodniowego przed upływem sześciu okresów 24h w dostępnych danych.",
            PeriodStartUtc = previousWeeklyRest.EndUtc,
            PeriodEndUtc = deadlineUtc,
            ActualMinutes = actualMinutes,
            LimitMinutes = WeeklyRestTimelineHelper.SixTwentyFourHourPeriodsMinutes,
            Metadata = new Dictionary<string, object>
            {
                ["actualRestMinutes"] = nextWeeklyRestStartUtc.HasValue ? WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes : 0,
                ["requiredRestMinutes"] = WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes,
                ["missingRestMinutes"] = nextWeeklyRestStartUtc.HasValue ? 0 : WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes,
                ["compensationDueDate"] = string.Empty,
                ["compensationDeadlineUtc"] = deadlineUtc,
                ["relatedWeeklyRestStartUtc"] = previousWeeklyRest.StartUtc,
                ["relatedWeeklyRestEndUtc"] = previousWeeklyRest.EndUtc,
                ["previousWeeklyRestEndUtc"] = previousWeeklyRest.EndUtc.Ticks,
                ["nextWeeklyRestStartUtc"] = nextWeeklyRestStartUtc?.Ticks ?? 0,
                ["deadlineUtc"] = deadlineUtc.Ticks,
                ["exceededMinutes"] = exceededMinutes
            }
        };
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
