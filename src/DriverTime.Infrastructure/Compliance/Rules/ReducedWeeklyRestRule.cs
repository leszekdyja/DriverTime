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

        var weeks = GetWeekRanges(timeline);
        var reducedWeeklyRests = 0;
        var weeksBelowReducedRest = 0;
        var maxWeeklyRest = TimeSpan.Zero;

        foreach (var week in weeks)
        {
            var longestRest = GetRestActivitiesForWeek(timeline, week)
                .Select(x => x.Duration)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();

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
                Metadata = new Dictionary<string, long>
                {
                    ["longestRestMinutes"] = restMinutes,
                    ["minimumReducedWeeklyRestMinutes"] = MinimumReducedWeeklyRestMinutes,
                    ["regularWeeklyRestMinutes"] = RegularWeeklyRestMinutes,
                    ["missingMinutes"] = Math.Max(0, MinimumReducedWeeklyRestMinutes - restMinutes)
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, reducedWeeklyRests={ReducedWeeklyRests}, weeksBelow24h={WeeksBelowReducedRest}, maxWeeklyRestMinutes={MaxWeeklyRestMinutes}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weeks.Count,
            reducedWeeklyRests,
            weeksBelowReducedRest,
            (long)Math.Round(maxWeeklyRest.TotalMinutes),
            result.Violations.Count);

        return result;
    }

    private static List<DateTime> GetWeekRanges(IReadOnlyList<TimelineActivity> timeline)
    {
        if (timeline.Count == 0)
        {
            return [];
        }

        var firstWeek = GetIsoWeekStart(timeline.Min(x => x.StartUtc));
        var lastWeek = GetIsoWeekStart(timeline.Max(x => x.EndUtc));
        var weeks = new List<DateTime>();

        for (var week = firstWeek; week <= lastWeek; week = week.AddDays(7))
        {
            weeks.Add(week);
        }

        return weeks;
    }

    private static IEnumerable<TimelineActivity> GetRestActivitiesForWeek(
        IReadOnlyList<TimelineActivity> timeline,
        DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(7);

        return timeline
            .Where(IsRest)
            .Where(x => x.StartUtc >= weekStart && x.StartUtc < weekEnd);
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

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
