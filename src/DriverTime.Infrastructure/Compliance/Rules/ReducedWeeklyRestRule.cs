using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ReducedWeeklyRestRule : IComplianceRule
{
    private const string RuleCode = "REDUCED_WEEKLY_REST";
    private const long MinimumReducedWeeklyRestMinutes = 24 * 60;
    private const long RegularWeeklyRestMinutes = 45 * 60;
    private const int PastActivityYearsLimit = 10;
    private const int FutureActivityDaysLimit = 1;
    private static readonly TimeSpan MinimumReducedWeeklyRest = TimeSpan.FromMinutes(MinimumReducedWeeklyRestMinutes);
    private static readonly TimeSpan RegularWeeklyRest = TimeSpan.FromMinutes(RegularWeeklyRestMinutes);

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

        var validTimeline = GetValidTimeline(timeline);
        var ignoredActivities = timeline.Count - validTimeline.Count;
        var weeks = GetActiveWeekRanges(validTimeline);
        var restPeriods = WeeklyRestTimelineHelper.BuildContinuousRestPeriods(validTimeline);
        var reducedWeeklyRests = 0;
        var weeksBelowReducedRest = 0;
        var maxWeeklyRest = TimeSpan.Zero;

        foreach (var week in weeks)
        {
            var longestRest = WeeklyRestTimelineHelper.GetLongestRestOverlappingWeek(restPeriods, week);

            maxWeeklyRest = Max(maxWeeklyRest, longestRest);

            if (longestRest >= RegularWeeklyRest)
            {
                continue;
            }

            if (longestRest >= MinimumReducedWeeklyRest)
            {
                reducedWeeklyRests++;
                continue;
            }

            weeksBelowReducedRest++;
            var restMinutes = (long)Math.Round(longestRest.TotalMinutes);

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Odpoczynek tygodniowy w tygodniu rozpoczynającym się {week:yyyy-MM-dd} był krótszy niż wymagane minimum 24 godziny. Najdłuższy odpoczynek wyniósł {FormatDuration(longestRest)}.",
                PeriodStartUtc = week,
                PeriodEndUtc = week.AddDays(7),
                ActualMinutes = restMinutes,
                LimitMinutes = MinimumReducedWeeklyRestMinutes,
                Metadata = new Dictionary<string, object>
                {
                    ["longestRestMinutes"] = restMinutes,
                    ["minimumReducedWeeklyRestMinutes"] = MinimumReducedWeeklyRestMinutes,
                    ["regularWeeklyRestMinutes"] = RegularWeeklyRestMinutes,
                    ["missingMinutes"] = Math.Max(0, MinimumReducedWeeklyRestMinutes - restMinutes)
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, ignoredSuspiciousActivities={IgnoredSuspiciousActivities}, reducedWeeklyRests={ReducedWeeklyRests}, weeksBelow24h={WeeksBelowReducedRest}, maxWeeklyRestMinutes={MaxWeeklyRestMinutes}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weeks.Count,
            ignoredActivities,
            reducedWeeklyRests,
            weeksBelowReducedRest,
            (long)Math.Round(maxWeeklyRest.TotalMinutes),
            result.Violations.Count);

        return result;
    }

    private static IReadOnlyList<TimelineActivity> GetValidTimeline(IReadOnlyList<TimelineActivity> timeline)
    {
        var nowUtc = DateTime.UtcNow;
        var earliestAllowedUtc = nowUtc.AddYears(-PastActivityYearsLimit);
        var latestAllowedUtc = nowUtc.AddDays(FutureActivityDaysLimit);

        return timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .Where(x => x.StartUtc >= earliestAllowedUtc && x.EndUtc <= latestAllowedUtc)
            .ToList();
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
            var firstWeek = GetIsoWeekStart(activity.StartUtc);
            var lastMoment = activity.EndUtc.AddTicks(-1);
            var lastWeek = GetIsoWeekStart(lastMoment > activity.StartUtc ? lastMoment : activity.StartUtc);

            for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
            {
                weeks.Add(week);
            }
        }

        return weeks
            .OrderBy(x => x)
            .ToList();
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

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
