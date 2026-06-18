using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class SixTwentyFourHourPeriodsRule : IComplianceRule
{
    private const string RuleCode = "SIX_24H_PERIODS";
    private const long WeeklyRestMinutes = 24 * 60;
    private const long SixTwentyFourHourPeriodsMinutes = 6 * 24 * 60;
    private static readonly TimeSpan WeeklyRest = TimeSpan.FromMinutes(WeeklyRestMinutes);

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

        var weeklyRests = WeeklyRestTimelineHelper.BuildContinuousRestPeriods(validTimeline)
            .Where(x => x.Duration >= WeeklyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();

        if (weeklyRests.Count == 0)
        {
            LogResult(driverId, weeklyRests.Count, checkedPeriods: 0, result.Violations.Count);
            return result;
        }

        var timelineEndUtc = validTimeline
            .Select(x => x.EndUtc)
            .DefaultIfEmpty()
            .Max();

        var checkedPeriods = 0;

        for (var index = 0; index < weeklyRests.Count; index++)
        {
            var previousWeeklyRest = weeklyRests[index];
            var deadlineUtc = previousWeeklyRest.EndUtc.AddMinutes(SixTwentyFourHourPeriodsMinutes);
            var nextWeeklyRest = weeklyRests
                .Skip(index + 1)
                .FirstOrDefault();

            if (nextWeeklyRest is not null)
            {
                checkedPeriods++;

                if (nextWeeklyRest.StartUtc <= deadlineUtc)
                {
                    continue;
                }

                result.Violations.Add(CreateViolation(
                    previousWeeklyRest,
                    nextWeeklyRest.StartUtc,
                    deadlineUtc,
                    comparisonUtc: nextWeeklyRest.StartUtc));

                continue;
            }

            if (timelineEndUtc != default && timelineEndUtc > deadlineUtc)
            {
                checkedPeriods++;

                result.Violations.Add(CreateViolation(
                    previousWeeklyRest,
                    nextWeeklyRestStartUtc: null,
                    deadlineUtc,
                    comparisonUtc: timelineEndUtc));
            }
        }

        LogResult(driverId, weeklyRests.Count, checkedPeriods, result.Violations.Count);

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
        WeeklyRestTimelineHelper.RestPeriod previousWeeklyRest,
        DateTime? nextWeeklyRestStartUtc,
        DateTime deadlineUtc,
        DateTime comparisonUtc)
    {
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
            LimitMinutes = SixTwentyFourHourPeriodsMinutes,
            Metadata = new Dictionary<string, long>
            {
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
