using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ReducedWeeklyRestCompensationRule : IComplianceRule
{
    private const string RuleCode = "REDUCED_WEEKLY_REST_COMPENSATION";
    private const long RegularWeeklyRestMinutes = 45 * 60;
    private const long MinimumReducedWeeklyRestMinutes = 24 * 60;
    private const long MinimumAttachedRestMinutes = 9 * 60;
    private static readonly TimeSpan RegularWeeklyRest = TimeSpan.FromMinutes(RegularWeeklyRestMinutes);
    private static readonly TimeSpan MinimumReducedWeeklyRest = TimeSpan.FromMinutes(MinimumReducedWeeklyRestMinutes);
    private static readonly TimeSpan MinimumAttachedRest = TimeSpan.FromMinutes(MinimumAttachedRestMinutes);

    public string Code => RuleCode;

    public string Name => "Reduced weekly rest compensation";

    private readonly ILogger<ReducedWeeklyRestCompensationRule> _logger;

    public ReducedWeeklyRestCompensationRule(ILogger<ReducedWeeklyRestCompensationRule> logger)
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

        var restPeriods = WeeklyRestTimelineHelper.BuildContinuousRestPeriods(timeline);
        var reducedWeeklyRests = restPeriods
            .Where(x => x.Duration >= MinimumReducedWeeklyRest && x.Duration < RegularWeeklyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();
        var timelineEndUtc = timeline.Count == 0
            ? (DateTime?)null
            : timeline.Max(x => x.EndUtc);
        var compensatedRests = 0;
        var missingCompensations = 0;
        var pendingCompensations = 0;

        foreach (var reducedRest in reducedWeeklyRests)
        {
            var compensationDebt = RegularWeeklyRest - reducedRest.Duration;
            var compensationDebtMinutes = (long)Math.Round(compensationDebt.TotalMinutes);
            var reductionWeekStart = GetIsoWeekStart(reducedRest.StartUtc);
            var compensationDeadlineUtc = reductionWeekStart.AddDays(28);
            var requiredCombinedRest = MinimumAttachedRest + compensationDebt;
            var possibleCompensations = restPeriods
                .Where(x => x.StartUtc >= reducedRest.EndUtc)
                .Where(x => x.EndUtc <= compensationDeadlineUtc)
                .Where(x => x.Duration >= MinimumAttachedRest)
                .OrderBy(x => x.StartUtc)
                .ToList();

            var fullCompensation = possibleCompensations
                .FirstOrDefault(x => x.Duration >= requiredCombinedRest);

            if (fullCompensation is not null)
            {
                compensatedRests++;
                continue;
            }

            var deadlineCoveredByTimeline = timelineEndUtc.HasValue && timelineEndUtc.Value >= compensationDeadlineUtc;
            if (!deadlineCoveredByTimeline)
            {
                pendingCompensations++;
                continue;
            }

            missingCompensations++;
            var foundCompensationMinutes = possibleCompensations
                .Select(x => Math.Max(0, (long)Math.Round((x.Duration - MinimumAttachedRest).TotalMinutes)))
                .DefaultIfEmpty(0)
                .Max();

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Nie znaleziono wymaganej kompensacji skróconego odpoczynku tygodniowego przed {compensationDeadlineUtc:yyyy-MM-dd}. Skrócony odpoczynek wyniósł {FormatDuration(reducedRest.Duration)}, a dług kompensacyjny wynosi {FormatDuration(compensationDebt)}.",
                PeriodStartUtc = reducedRest.StartUtc,
                PeriodEndUtc = compensationDeadlineUtc,
                ActualMinutes = foundCompensationMinutes,
                LimitMinutes = compensationDebtMinutes,
                Metadata = new Dictionary<string, long>
                {
                    ["reducedRestStartUtc"] = reducedRest.StartUtc.Ticks,
                    ["reducedRestEndUtc"] = reducedRest.EndUtc.Ticks,
                    ["reducedRestMinutes"] = (long)Math.Round(reducedRest.Duration.TotalMinutes),
                    ["requiredRegularWeeklyRestMinutes"] = RegularWeeklyRestMinutes,
                    ["compensationDebtMinutes"] = compensationDebtMinutes,
                    ["compensationDeadlineUtc"] = compensationDeadlineUtc.Ticks,
                    ["foundCompensationMinutes"] = foundCompensationMinutes,
                    ["minimumAttachedRestMinutes"] = MinimumAttachedRestMinutes
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: reducedWeeklyRests={ReducedWeeklyRests}, compensatedRests={CompensatedRests}, pendingCompensations={PendingCompensations}, missingCompensations={MissingCompensations}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            reducedWeeklyRests.Count,
            compensatedRests,
            pendingCompensations,
            missingCompensations,
            result.Violations.Count);

        return result;
    }

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
