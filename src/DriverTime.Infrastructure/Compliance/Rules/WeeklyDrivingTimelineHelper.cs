using System.Globalization;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

internal static class WeeklyDrivingTimelineHelper
{
    public static IReadOnlyDictionary<DateTime, TimeSpan> GetDrivingByIsoWeek(
        IReadOnlyList<TimelineActivity> timeline)
    {
        return GetDrivingByPeriod(timeline, GetIsoWeekStart, periodStart => periodStart.AddDays(7));
    }

    public static IReadOnlyDictionary<DateTime, TimeSpan> GetDrivingByUtcDay(
        IReadOnlyList<TimelineActivity> timeline)
    {
        return GetDrivingByPeriod(
            timeline,
            value => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc),
            periodStart => periodStart.AddDays(1));
    }

    public static IReadOnlyList<TimelineActivity> GetMergedDrivingTimeline(
        IReadOnlyList<TimelineActivity> timeline)
    {
        return GetEffectiveDrivingSegments(timeline)
            .Select(x => new TimelineActivity
            {
                SourceActivityId = x.SourceActivityId,
                DriverId = x.DriverId,
                ActivityType = ActivityTypeNormalizer.Driving,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc
            })
            .ToList();
    }

    private static IReadOnlyDictionary<DateTime, TimeSpan> GetDrivingByPeriod(
        IReadOnlyList<TimelineActivity> timeline,
        Func<DateTime, DateTime> getPeriodStart,
        Func<DateTime, DateTime> getPeriodEnd)
    {
        var segmentsByPeriod = new Dictionary<DateTime, List<DrivingSegment>>();

        foreach (var activity in GetEffectiveDrivingSegments(timeline))
        {
            var segmentStart = activity.StartUtc;

            while (segmentStart < activity.EndUtc)
            {
                var periodStart = getPeriodStart(segmentStart);
                var periodEnd = getPeriodEnd(periodStart);
                var segmentEnd = activity.EndUtc < periodEnd
                    ? activity.EndUtc
                    : periodEnd;

                if (!segmentsByPeriod.TryGetValue(periodStart, out var segments))
                {
                    segments = new List<DrivingSegment>();
                    segmentsByPeriod[periodStart] = segments;
                }

                segments.Add(new DrivingSegment(
                    segmentStart,
                    segmentEnd,
                    activity.SourceActivityId,
                    activity.DriverId));
                segmentStart = segmentEnd;
            }
        }

        return segmentsByPeriod.ToDictionary(
            x => x.Key,
            x => MergeDrivingSegments(x.Value)
                .Aggregate(TimeSpan.Zero, (sum, segment) => sum + (segment.EndUtc - segment.StartUtc)));
    }

    private static bool IsDriving(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<DrivingSegment> GetEffectiveDrivingSegments(
        IReadOnlyList<TimelineActivity> timeline)
    {
        var drivingSegments = timeline
            .Where(IsDriving)
            .Where(x => x.StartUtc < x.EndUtc)
            .Select(x => new DrivingSegment(x.StartUtc, x.EndUtc, x.SourceActivityId, x.DriverId))
            .ToList();

        if (drivingSegments.Count == 0)
        {
            return Array.Empty<DrivingSegment>();
        }

        var nonDrivingSegments = MergeDrivingSegments(timeline
            .Where(x => !IsDriving(x))
            .Where(x => x.StartUtc < x.EndUtc)
            .Select(x => new DrivingSegment(x.StartUtc, x.EndUtc, Guid.Empty, x.DriverId)));

        if (nonDrivingSegments.Count == 0)
        {
            return MergeDrivingSegments(drivingSegments);
        }

        var effectiveDriving = drivingSegments
            .SelectMany(x => SubtractOverlaps(x, nonDrivingSegments))
            .ToList();

        return MergeDrivingSegments(effectiveDriving);
    }

    private static DateTime GetIsoWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);

        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }

    private static IReadOnlyList<DrivingSegment> MergeDrivingSegments(
        IEnumerable<DrivingSegment> segments)
    {
        var ordered = segments
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<DrivingSegment>();
        }

        var merged = new List<DrivingSegment>();
        var current = ordered[0];

        foreach (var segment in ordered.Skip(1))
        {
            if (segment.StartUtc <= current.EndUtc)
            {
                current = current with
                {
                    EndUtc = segment.EndUtc > current.EndUtc
                        ? segment.EndUtc
                        : current.EndUtc
                };
                continue;
            }

            merged.Add(current);
            current = segment;
        }

        merged.Add(current);

        return merged;
    }

    private static IEnumerable<DrivingSegment> SubtractOverlaps(
        DrivingSegment driving,
        IReadOnlyList<DrivingSegment> blockers)
    {
        var remaining = new List<DrivingSegment> { driving };

        foreach (var blocker in blockers)
        {
            if (blocker.EndUtc <= driving.StartUtc)
            {
                continue;
            }

            if (blocker.StartUtc >= driving.EndUtc)
            {
                break;
            }

            var next = new List<DrivingSegment>();

            foreach (var segment in remaining)
            {
                if (blocker.EndUtc <= segment.StartUtc ||
                    blocker.StartUtc >= segment.EndUtc)
                {
                    next.Add(segment);
                    continue;
                }

                if (blocker.StartUtc > segment.StartUtc)
                {
                    next.Add(segment with
                    {
                        EndUtc = blocker.StartUtc
                    });
                }

                if (blocker.EndUtc < segment.EndUtc)
                {
                    next.Add(segment with
                    {
                        StartUtc = blocker.EndUtc
                    });
                }
            }

            remaining = next;

            if (remaining.Count == 0)
            {
                break;
            }
        }

        return remaining;
    }

    private sealed record DrivingSegment(
        DateTime StartUtc,
        DateTime EndUtc,
        Guid SourceActivityId,
        Guid DriverId);
}
