using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ReducedWeeklyRestCompensationRule : IComplianceRule
{
    private const string RuleCode = "REDUCED_WEEKLY_REST_COMPENSATION";

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

        var validTimeline = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
        var restPeriods = WeeklyRestTimelineHelper.BuildContinuousRestPeriods(validTimeline);
        var reducedWeeklyRests = restPeriods
            .Where(WeeklyRestTimelineHelper.IsReducedWeeklyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();
        var timelineEndUtc = validTimeline
            .Select(x => x.EndUtc)
            .DefaultIfEmpty()
            .Max();
        var compensatedRests = 0;
        var missingCompensations = 0;
        var pendingCompensations = 0;

        foreach (var reducedRest in reducedWeeklyRests)
        {
            var compensationDebt = WeeklyRestTimelineHelper.RegularWeeklyRest - reducedRest.Duration;
            var compensationDebtMinutes = ToMinutes(compensationDebt);
            var compensationDeadlineUtc = WeeklyRestTimelineHelper.GetCompensationDeadlineUtc(reducedRest);
            var requiredCombinedRest = WeeklyRestTimelineHelper.MinimumAttachedRest + compensationDebt;
            var possibleCompensations = restPeriods
                .Where(x => x.StartUtc >= reducedRest.EndUtc)
                .Where(x => x.EndUtc <= compensationDeadlineUtc)
                .Where(x => x.Duration >= WeeklyRestTimelineHelper.MinimumAttachedRest)
                .OrderBy(x => x.StartUtc)
                .ToList();

            var fullCompensation = possibleCompensations
                .FirstOrDefault(x => x.Duration >= requiredCombinedRest);

            if (fullCompensation is not null)
            {
                compensatedRests++;
                continue;
            }

            var deadlineCoveredByTimeline = timelineEndUtc != default && timelineEndUtc >= compensationDeadlineUtc;
            if (!deadlineCoveredByTimeline)
            {
                pendingCompensations++;
                continue;
            }

            var foundCompensationMinutes = possibleCompensations
                .Select(x => Math.Max(0, ToMinutes(x.Duration - WeeklyRestTimelineHelper.MinimumAttachedRest)))
                .DefaultIfEmpty(0)
                .Max();
            var missingRestMinutes = Math.Max(0, compensationDebtMinutes - foundCompensationMinutes);

            missingCompensations++;

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Nie znaleziono wymaganej kompensacji skroconego odpoczynku tygodniowego przed {compensationDeadlineUtc:yyyy-MM-dd}. Skrocony odpoczynek wyniosl {FormatDuration(reducedRest.Duration)}, a dlug kompensacyjny wynosi {FormatDuration(compensationDebt)}.",
                PeriodStartUtc = reducedRest.StartUtc,
                PeriodEndUtc = compensationDeadlineUtc,
                ActualMinutes = foundCompensationMinutes,
                LimitMinutes = compensationDebtMinutes,
                Metadata = new Dictionary<string, object>
                {
                    ["actualRestMinutes"] = ToMinutes(reducedRest.Duration),
                    ["requiredRestMinutes"] = WeeklyRestTimelineHelper.RegularWeeklyRestMinutes,
                    ["missingRestMinutes"] = missingRestMinutes,
                    ["compensationDueDate"] = compensationDeadlineUtc.ToString("yyyy-MM-dd"),
                    ["compensationDeadlineUtc"] = compensationDeadlineUtc,
                    ["relatedWeeklyRestStartUtc"] = reducedRest.StartUtc,
                    ["relatedWeeklyRestEndUtc"] = reducedRest.EndUtc,
                    ["reducedRestStartUtc"] = reducedRest.StartUtc.Ticks,
                    ["reducedRestEndUtc"] = reducedRest.EndUtc.Ticks,
                    ["reducedRestMinutes"] = ToMinutes(reducedRest.Duration),
                    ["requiredRegularWeeklyRestMinutes"] = WeeklyRestTimelineHelper.RegularWeeklyRestMinutes,
                    ["compensationDebtMinutes"] = compensationDebtMinutes,
                    ["foundCompensationMinutes"] = foundCompensationMinutes,
                    ["minimumAttachedRestMinutes"] = WeeklyRestTimelineHelper.MinimumAttachedRestMinutes
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

    private static long ToMinutes(TimeSpan duration) =>
        (long)Math.Round(duration.TotalMinutes);

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
