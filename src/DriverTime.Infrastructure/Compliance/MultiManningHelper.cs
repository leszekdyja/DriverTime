using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance;

internal static class MultiManningHelper
{
    public enum CrewStatus { Single, Confirmed, Uncertain }

    public static bool IsCoDriverBreak(TimelineActivity activity) =>
        ActivityTypeNormalizer.Normalize(activity.ActivityType) == ActivityTypeNormalizer.Availability
        && activity.IsCrew == true && activity.CardSlot == 2
        && activity.Duration >= TimeSpan.FromMinutes(45);

    public static IReadOnlyList<TimelineActivity> MergeAdjacentBreaks(IEnumerable<TimelineActivity> timeline)
    {
        var result = new List<TimelineActivity>();
        foreach (var activity in timeline.OrderBy(x => x.StartUtc).ThenBy(x => x.EndUtc))
        {
            var previous = result.LastOrDefault();
            var type = ActivityTypeNormalizer.Normalize(activity.ActivityType);
            if (previous is not null && previous.EndUtc == activity.StartUtc
                && ActivityTypeNormalizer.Normalize(previous.ActivityType) == type
                && (type == ActivityTypeNormalizer.Rest
                    || (type == ActivityTypeNormalizer.Availability && activity.IsCrew == true
                        && previous.IsCrew == true && activity.CardSlot == 2 && previous.CardSlot == 2)))
            {
                result[^1] = new TimelineActivity
                {
                    SourceActivityId = previous.SourceActivityId, DriverId = previous.DriverId,
                    ActivityType = type, StartUtc = previous.StartUtc, EndUtc = activity.EndUtc,
                    IsCrew = previous.IsCrew, CardSlot = previous.CardSlot
                };
            }
            else result.Add(activity);
        }
        return result;
    }

    public static CrewStatus GetStatus(
        IReadOnlyList<TimelineActivity> timeline, DateTime dutyStart, DateTime dutyEnd)
    {
        var activities = timeline.Where(x => x.StartUtc < dutyEnd && x.EndUtc > dutyStart)
            .OrderBy(x => x.StartUtc).ToList();
        var travelling = activities.Where(x => ActivityTypeNormalizer.Normalize(x.ActivityType)
            is ActivityTypeNormalizer.Driving or ActivityTypeNormalizer.Availability).ToList();
        var firstCrew = travelling.FirstOrDefault(x => x.IsCrew == true);
        if (firstCrew is null) return CrewStatus.Single;

        var firstHourEnd = dutyStart.AddHours(1);
        if (firstCrew.StartUtc > firstHourEnd) return CrewStatus.Single;
        if (travelling.Any(x => x.EndUtc > firstHourEnd && x.IsCrew == false
            && ActivityTypeNormalizer.Normalize(x.ActivityType) == ActivityTypeNormalizer.Driving))
            return CrewStatus.Single;

        // Missing crew flags in old downloads are not evidence of a full crew duty.
        if (travelling.Any(x => x.EndUtc > firstHourEnd && x.IsCrew != true)
            || activities.Any(x => ActivityTypeNormalizer.Normalize(x.ActivityType) == ActivityTypeNormalizer.Unknown))
            return CrewStatus.Uncertain;

        var coveredUntil = dutyStart;
        foreach (var activity in activities)
        {
            if (activity.StartUtc > coveredUntil) return CrewStatus.Uncertain;
            if (activity.EndUtc > coveredUntil) coveredUntil = activity.EndUtc;
        }
        return coveredUntil < dutyEnd ? CrewStatus.Uncertain : CrewStatus.Confirmed;
    }
}
