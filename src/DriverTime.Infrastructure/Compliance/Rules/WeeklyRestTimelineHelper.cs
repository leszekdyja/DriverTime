using System.Globalization;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

internal static class WeeklyRestTimelineHelper
{
    public const long MinimumReducedWeeklyRestMinutes = 24 * 60;
    public const long RegularWeeklyRestMinutes = 45 * 60;
    public const long SixTwentyFourHourPeriodsMinutes = 6 * 24 * 60;
    public const long MinimumAttachedRestMinutes = 9 * 60;

    public static readonly TimeSpan MinimumReducedWeeklyRest =
        TimeSpan.FromMinutes(MinimumReducedWeeklyRestMinutes);
    public static readonly TimeSpan RegularWeeklyRest =
        TimeSpan.FromMinutes(RegularWeeklyRestMinutes);
    public static readonly TimeSpan SixTwentyFourHourPeriods =
        TimeSpan.FromMinutes(SixTwentyFourHourPeriodsMinutes);
    public static readonly TimeSpan MinimumAttachedRest =
        TimeSpan.FromMinutes(MinimumAttachedRestMinutes);

    public static IReadOnlyList<RestPeriod> BuildContinuousRestPeriods(IReadOnlyList<TimelineActivity> timeline)
    {
        var ordered = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var restPeriods = new List<RestPeriod>();

        for (var index = 0; index < ordered.Count; index++)
        {
            var activity = ordered[index];

            if (IsRestCompatible(activity))
            {
                restPeriods.Add(new RestPeriod(activity.StartUtc, activity.EndUtc));
            }

            if (index + 1 >= ordered.Count)
            {
                continue;
            }

            var next = ordered[index + 1];
            if (activity.EndUtc < next.StartUtc)
            {
                restPeriods.Add(new RestPeriod(activity.EndUtc, next.StartUtc));
            }
        }

        return MergeRestPeriods(restPeriods);
    }

    public static IReadOnlyList<RestPeriod> BuildWeeklyRestPeriods(IReadOnlyList<TimelineActivity> timeline)
    {
        return BuildContinuousRestPeriods(timeline)
            .Where(x => x.Duration >= MinimumReducedWeeklyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();
    }

    public static TimeSpan GetLongestRestOverlappingWeek(
        IReadOnlyList<RestPeriod> restPeriods,
        DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(7);

        return restPeriods
            .Where(x => x.StartUtc < weekEnd && x.EndUtc > weekStart)
            .Select(x => x.Duration)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();
    }

    public static IReadOnlyList<SixPeriodViolation> FindSixPeriodViolations(
        IReadOnlyList<TimelineActivity> timeline)
    {
        var validTimeline = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
        var weeklyRests = BuildWeeklyRestPeriods(validTimeline);
        var violations = new List<SixPeriodViolation>();

        if (weeklyRests.Count == 0)
        {
            return violations;
        }

        var timelineEndUtc = validTimeline
            .Select(x => x.EndUtc)
            .DefaultIfEmpty()
            .Max();

        for (var index = 0; index < weeklyRests.Count; index++)
        {
            var previousRest = weeklyRests[index];
            var deadlineUtc = previousRest.EndUtc.Add(SixTwentyFourHourPeriods);
            var nextRest = weeklyRests.Skip(index + 1).FirstOrDefault();

            if (nextRest is not null)
            {
                if (nextRest.StartUtc > deadlineUtc)
                {
                    violations.Add(new SixPeriodViolation(
                        previousRest,
                        nextRest.StartUtc,
                        deadlineUtc,
                        nextRest.StartUtc));
                }

                continue;
            }

            if (timelineEndUtc != default && timelineEndUtc > deadlineUtc)
            {
                violations.Add(new SixPeriodViolation(
                    previousRest,
                    null,
                    deadlineUtc,
                    timelineEndUtc));
            }
        }

        return violations;
    }

    public static DateTime GetIsoWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);

        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }

    public static DateTime GetCompensationDeadlineUtc(RestPeriod reducedRest)
    {
        var reductionWeekStart = GetIsoWeekStart(reducedRest.StartUtc);

        return reductionWeekStart.AddDays(28);
    }

    public static bool IsRegularWeeklyRest(RestPeriod rest) =>
        rest.Duration >= RegularWeeklyRest;

    public static bool IsReducedWeeklyRest(RestPeriod rest) =>
        rest.Duration >= MinimumReducedWeeklyRest &&
        rest.Duration < RegularWeeklyRest;

    private static IReadOnlyList<RestPeriod> MergeRestPeriods(IEnumerable<RestPeriod> periods)
    {
        var ordered = periods
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var merged = new List<RestPeriod>();

        foreach (var period in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(period);
                continue;
            }

            var previous = merged[^1];
            if (period.StartUtc <= previous.EndUtc)
            {
                merged[^1] = previous with
                {
                    EndUtc = Max(previous.EndUtc, period.EndUtc)
                };
                continue;
            }

            merged.Add(period);
        }

        return merged;
    }

    private static bool IsRestCompatible(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase);

    private static DateTime Max(DateTime left, DateTime right) =>
        left >= right ? left : right;

    internal sealed record RestPeriod(DateTime StartUtc, DateTime EndUtc)
    {
        public TimeSpan Duration => EndUtc > StartUtc
            ? EndUtc - StartUtc
            : TimeSpan.Zero;
    }

    internal sealed record SixPeriodViolation(
        RestPeriod PreviousWeeklyRest,
        DateTime? NextWeeklyRestStartUtc,
        DateTime DeadlineUtc,
        DateTime ComparisonUtc);
}
