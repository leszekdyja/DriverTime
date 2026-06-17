using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class WeeklyRestCompensationRule : IComplianceRule
{
    private const string RuleCode = "WEEKLY_REST_COMPENSATION";
    private const long RegularWeeklyRestMinutes = 45 * 60;
    private const long MinimumReducedWeeklyRestMinutes = 24 * 60;
    private const long MinimumAttachedRestMinutes = 9 * 60;
    private static readonly TimeSpan RegularWeeklyRest = TimeSpan.FromMinutes(RegularWeeklyRestMinutes);
    private static readonly TimeSpan MinimumReducedWeeklyRest = TimeSpan.FromMinutes(MinimumReducedWeeklyRestMinutes);

    public string Code => RuleCode;

    public string Name => "Weekly rest compensation";

    private readonly ILogger<WeeklyRestCompensationRule> _logger;

    public WeeklyRestCompensationRule(ILogger<WeeklyRestCompensationRule> logger)
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

        var restPeriods = timeline
            .Where(IsRest)
            .OrderBy(x => x.StartUtc)
            .Select(x => new RestPeriod(x.StartUtc, x.EndUtc, x.Duration))
            .ToList();
        var reducedWeeklyRests = restPeriods
            .Where(x => x.Duration >= MinimumReducedWeeklyRest && x.Duration < RegularWeeklyRest)
            .ToList();
        var timelineEndUtc = timeline.Count == 0
            ? (DateTime?)null
            : timeline.Max(x => x.EndUtc);
        var compensatedRests = 0;
        var pendingCompensations = 0;
        var missingCompensations = 0;

        foreach (var reducedRest in reducedWeeklyRests)
        {
            var missing = RegularWeeklyRest - reducedRest.Duration;
            var missingMinutes = (long)Math.Round(missing.TotalMinutes);
            var reductionWeekStart = GetIsoWeekStart(reducedRest.StartUtc);
            var deadlineUtc = reductionWeekStart.AddDays(28);
            var requiredCombinedRest = TimeSpan.FromMinutes(MinimumAttachedRestMinutes) + missing;

            var compensation = restPeriods
                .Where(x => x.StartUtc > reducedRest.EndUtc)
                .Where(x => x.EndUtc <= deadlineUtc)
                .Where(x => x.Duration >= requiredCombinedRest)
                .OrderBy(x => x.StartUtc)
                .FirstOrDefault();

            if (compensation is not null)
            {
                compensatedRests++;
                continue;
            }

            var deadlineCoveredByTimeline = timelineEndUtc.HasValue && timelineEndUtc.Value >= deadlineUtc;
            var severity = deadlineCoveredByTimeline ? "High" : "Medium";

            if (deadlineCoveredByTimeline)
            {
                missingCompensations++;
            }
            else
            {
                pendingCompensations++;
            }

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = severity,
                Description = BuildDescription(reducedRest.Duration, missing, deadlineUtc, deadlineCoveredByTimeline),
                PeriodStartUtc = reducedRest.StartUtc,
                PeriodEndUtc = deadlineUtc,
                ActualMinutes = 0,
                LimitMinutes = missingMinutes,
                Metadata = new Dictionary<string, long>
                {
                    ["reducedWeeklyRestMinutes"] = (long)Math.Round(reducedRest.Duration.TotalMinutes),
                    ["missingCompensationMinutes"] = missingMinutes,
                    ["minimumAttachedRestMinutes"] = MinimumAttachedRestMinutes,
                    ["requiredCombinedRestMinutes"] = (long)Math.Round(requiredCombinedRest.TotalMinutes)
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

    private static string BuildDescription(
        TimeSpan reducedRest,
        TimeSpan missing,
        DateTime deadlineUtc,
        bool deadlineCoveredByTimeline)
    {
        var status = deadlineCoveredByTimeline
            ? "Nie znaleziono wymaganej rekompensaty."
            : "Nie znaleziono jeszcze wymaganej rekompensaty w dostępnych danych.";

        return $"{status} Skrócony odpoczynek tygodniowy wyniósł {FormatDuration(reducedRest)}, więc rekompensata {FormatDuration(missing)} musi być odebrana jednorazowo z innym odpoczynkiem minimum 9 godzin przed {deadlineUtc:yyyy-MM-dd}.";
    }

    private static bool IsRest(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase);

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

    private sealed record RestPeriod(DateTime StartUtc, DateTime EndUtc, TimeSpan Duration);
}
