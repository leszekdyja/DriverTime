using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

internal static class WeeklyRestTimelineHelper
{
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
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase) ||
        activity.ActivityType.Equals(ActivityTypeNormalizer.Availability, StringComparison.OrdinalIgnoreCase);

    private static DateTime Max(DateTime left, DateTime right) =>
        left >= right ? left : right;

    internal sealed record RestPeriod(DateTime StartUtc, DateTime EndUtc)
    {
        public TimeSpan Duration => EndUtc > StartUtc
            ? EndUtc - StartUtc
            : TimeSpan.Zero;
    }
}
