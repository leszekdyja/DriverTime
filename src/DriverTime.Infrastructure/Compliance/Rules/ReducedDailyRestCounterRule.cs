using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ReducedDailyRestCounterRule : IComplianceRule
{
    private const string RuleCode = "REDUCED_DAILY_REST_COUNTER";
    private const long FirstSplitDailyRestMinutes = 3 * 60;
    private const long ReducedDailyRestMinutes = 9 * 60;
    private const long RegularDailyRestMinutes = 11 * 60;
    private const long WeeklyRestMinutes = 24 * 60;
    private const long AllowedReducedDailyRestCount = 3;
    private static readonly TimeSpan FirstSplitDailyRest = TimeSpan.FromMinutes(FirstSplitDailyRestMinutes);
    private static readonly TimeSpan ReducedDailyRest = TimeSpan.FromMinutes(ReducedDailyRestMinutes);
    private static readonly TimeSpan RegularDailyRest = TimeSpan.FromMinutes(RegularDailyRestMinutes);
    private static readonly TimeSpan WeeklyRest = TimeSpan.FromMinutes(WeeklyRestMinutes);

    public string Code => RuleCode;

    public string Name => "Reduced daily rest counter";

    private readonly ILogger<ReducedDailyRestCounterRule> _logger;

    public ReducedDailyRestCounterRule(ILogger<ReducedDailyRestCounterRule> logger)
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

        var restPeriods = WeeklyRestTimelineHelper.BuildContinuousRestPeriods(timeline)
            .OrderBy(x => x.StartUtc)
            .ToList();

        if (restPeriods.Count == 0)
        {
            LogSummary(driverId, periods: 0, reducedDailyRests: 0, violations: 0);
            return result;
        }

        DateTime? periodStartUtc = null;
        var reducedDailyRestCount = 0;
        var totalReducedDailyRests = 0;
        var restsSinceWeeklyRest = new List<WeeklyRestTimelineHelper.RestPeriod>();
        var periods = 0;

        foreach (var rest in restPeriods)
        {
            if (rest.Duration >= WeeklyRest)
            {
                periods++;
                periodStartUtc = rest.EndUtc;
                reducedDailyRestCount = 0;
                restsSinceWeeklyRest.Clear();
                continue;
            }

            periodStartUtc ??= restPeriods[0].StartUtc;

            if (IsReducedDailyRest(rest) && !IsSecondPartOfSplitRegularRest(rest, restsSinceWeeklyRest))
            {
                reducedDailyRestCount++;
                totalReducedDailyRests++;

                if (reducedDailyRestCount > AllowedReducedDailyRestCount)
                {
                    result.Violations.Add(CreateViolation(
                        periodStartUtc.Value,
                        rest.EndUtc,
                        reducedDailyRestCount,
                        rest));
                }
            }

            restsSinceWeeklyRest.Add(rest);
        }

        LogSummary(driverId, periods, totalReducedDailyRests, result.Violations.Count);

        return result;
    }

    private static ComplianceViolationCandidate CreateViolation(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        long reducedDailyRestCount,
        WeeklyRestTimelineHelper.RestPeriod violatingRest)
    {
        var violatingRestMinutes = (long)Math.Round(violatingRest.Duration.TotalMinutes);

        return new ComplianceViolationCandidate
        {
            Code = RuleCode,
            RuleName = "Reduced daily rest counter",
            Severity = "High",
            Description = $"Przekroczono limit 3 skróconych odpoczynków dziennych pomiędzy odpoczynkami tygodniowymi. Wykryto {reducedDailyRestCount}. skrócony odpoczynek dzienny.",
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            ActualMinutes = reducedDailyRestCount,
            LimitMinutes = AllowedReducedDailyRestCount,
            Metadata = new Dictionary<string, object>
            {
                ["periodStartUtc"] = periodStartUtc.Ticks,
                ["periodEndUtc"] = periodEndUtc.Ticks,
                ["reducedDailyRestCount"] = reducedDailyRestCount,
                ["allowedReducedDailyRestCount"] = AllowedReducedDailyRestCount,
                ["violatingRestStartUtc"] = violatingRest.StartUtc.Ticks,
                ["violatingRestEndUtc"] = violatingRest.EndUtc.Ticks,
                ["violatingRestMinutes"] = violatingRestMinutes
            }
        };
    }

    private static bool IsReducedDailyRest(WeeklyRestTimelineHelper.RestPeriod rest) =>
        rest.Duration >= ReducedDailyRest && rest.Duration < RegularDailyRest;

    private static bool IsSecondPartOfSplitRegularRest(
        WeeklyRestTimelineHelper.RestPeriod rest,
        IReadOnlyList<WeeklyRestTimelineHelper.RestPeriod> previousRests)
    {
        if (rest.Duration < ReducedDailyRest)
        {
            return false;
        }

        return previousRests.Any(previous =>
            previous.EndUtc <= rest.StartUtc &&
            previous.Duration >= FirstSplitDailyRest &&
            previous.Duration < ReducedDailyRest);
    }

    private void LogSummary(
        Guid driverId,
        int periods,
        int reducedDailyRests,
        int violations)
    {
        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: periods={Periods}, reducedDailyRests={ReducedDailyRests}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            periods,
            reducedDailyRests,
            violations);
    }
}
