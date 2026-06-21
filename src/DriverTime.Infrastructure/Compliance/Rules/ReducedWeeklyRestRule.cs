using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ReducedWeeklyRestRule : IComplianceRule
{
    private const string RuleCode = "REDUCED_WEEKLY_REST";

    public string Code => RuleCode;

    public string Name => "Reduced weekly rest";

    private readonly ILogger<ReducedWeeklyRestRule> _logger;

    public ReducedWeeklyRestRule(ILogger<ReducedWeeklyRestRule> logger)
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
        var activeWeeks = GetActiveWeekRanges(validTimeline);
        var restPeriods = WeeklyRestTimelineHelper.BuildContinuousRestPeriods(validTimeline);
        var weeklyRests = restPeriods
            .Where(x => x.Duration >= WeeklyRestTimelineHelper.MinimumReducedWeeklyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();
        var reducedWeeklyRests = weeklyRests.Count(WeeklyRestTimelineHelper.IsReducedWeeklyRest);
        var regularWeeklyRests = weeklyRests.Count(WeeklyRestTimelineHelper.IsRegularWeeklyRest);
        var weeksBelowReducedRest = 0;

        foreach (var weekStartUtc in activeWeeks)
        {
            if (weeklyRests.Any(x => x.StartUtc < weekStartUtc.AddDays(7) && x.EndUtc > weekStartUtc))
            {
                continue;
            }

            var longestRest = WeeklyRestTimelineHelper.GetLongestRestOverlappingWeek(restPeriods, weekStartUtc);
            var actualRestMinutes = ToMinutes(longestRest);
            var missingRestMinutes = Math.Max(0, WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes - actualRestMinutes);

            weeksBelowReducedRest++;

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Odpoczynek tygodniowy w tygodniu rozpoczynajacym sie {weekStartUtc:yyyy-MM-dd} byl krotszy niz wymagane minimum 24 godziny. Najdluzszy odpoczynek wyniosl {FormatDuration(longestRest)}.",
                PeriodStartUtc = weekStartUtc,
                PeriodEndUtc = weekStartUtc.AddDays(7),
                ActualMinutes = actualRestMinutes,
                LimitMinutes = WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes,
                Metadata = new Dictionary<string, object>
                {
                    ["actualRestMinutes"] = actualRestMinutes,
                    ["requiredRestMinutes"] = WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes,
                    ["missingRestMinutes"] = missingRestMinutes,
                    ["compensationDueDate"] = string.Empty,
                    ["compensationDeadlineUtc"] = string.Empty,
                    ["relatedWeeklyRestStartUtc"] = string.Empty,
                    ["relatedWeeklyRestEndUtc"] = string.Empty,
                    ["longestRestMinutes"] = actualRestMinutes,
                    ["minimumReducedWeeklyRestMinutes"] = WeeklyRestTimelineHelper.MinimumReducedWeeklyRestMinutes,
                    ["regularWeeklyRestMinutes"] = WeeklyRestTimelineHelper.RegularWeeklyRestMinutes,
                    ["missingMinutes"] = missingRestMinutes
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, weeklyRests={WeeklyRests}, regularWeeklyRests={RegularWeeklyRests}, reducedWeeklyRests={ReducedWeeklyRests}, weeksBelow24h={WeeksBelowReducedRest}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            activeWeeks.Count,
            weeklyRests.Count,
            regularWeeklyRests,
            reducedWeeklyRests,
            weeksBelowReducedRest,
            result.Violations.Count);

        return result;
    }

    private static List<DateTime> GetActiveWeekRanges(IReadOnlyList<TimelineActivity> timeline)
    {
        if (timeline.Count == 0)
        {
            return [];
        }

        var weeks = new HashSet<DateTime>();

        foreach (var activity in timeline)
        {
            var firstWeek = WeeklyRestTimelineHelper.GetIsoWeekStart(activity.StartUtc);
            var lastMoment = activity.EndUtc.AddTicks(-1);
            var lastWeek = WeeklyRestTimelineHelper.GetIsoWeekStart(lastMoment > activity.StartUtc ? lastMoment : activity.StartUtc);

            for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
            {
                weeks.Add(week);
            }
        }

        return weeks
            .OrderBy(x => x)
            .ToList();
    }

    private static long ToMinutes(TimeSpan duration) =>
        (long)Math.Round(duration.TotalMinutes);

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
