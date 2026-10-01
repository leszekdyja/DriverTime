namespace DriverTime.Infrastructure.Services;

internal static class ActivityIntervalAggregationHelper
{
    public static IReadOnlyList<ActivityInterval> ClipAndMergeByType(
        IEnumerable<ActivityInterval> activities,
        DateTime? rangeStartUtc = null,
        DateTime? rangeEndUtc = null)
    {
        return activities
            .Select(activity => Clip(activity, rangeStartUtc, rangeEndUtc))
            .Where(activity => activity is not null)
            .Select(activity => activity!)
            .GroupBy(activity => NormalizeActivityType(activity.ActivityType))
            .SelectMany(MergeGroup)
            .OrderBy(activity => activity.StartUtc)
            .ThenBy(activity => activity.EndUtc)
            .ToList();
    }

    public static long GetDurationSeconds(DateTime startUtc, DateTime endUtc)
    {
        return endUtc > startUtc
            ? (long)(endUtc - startUtc).TotalSeconds
            : 0;
    }

    public static string NormalizeActivityType(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static ActivityInterval? Clip(
        ActivityInterval activity,
        DateTime? rangeStartUtc,
        DateTime? rangeEndUtc)
    {
        var startUtc = rangeStartUtc.HasValue && activity.StartUtc < rangeStartUtc.Value
            ? rangeStartUtc.Value
            : activity.StartUtc;
        var endUtc = rangeEndUtc.HasValue && activity.EndUtc > rangeEndUtc.Value
            ? rangeEndUtc.Value
            : activity.EndUtc;

        return endUtc > startUtc
            ? activity with { StartUtc = startUtc, EndUtc = endUtc }
            : null;
    }

    private static IReadOnlyList<ActivityInterval> MergeGroup(
        IGrouping<string, ActivityInterval> group)
    {
        var ordered = group
            .Where(activity => activity.StartUtc < activity.EndUtc)
            .OrderBy(activity => activity.StartUtc)
            .ThenBy(activity => activity.EndUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<ActivityInterval>();
        }

        var merged = new List<ActivityInterval>();
        var current = ordered[0] with { ActivityType = group.Key };

        foreach (var activity in ordered.Skip(1))
        {
            if (activity.StartUtc <= current.EndUtc)
            {
                current = current with
                {
                    EndUtc = activity.EndUtc > current.EndUtc
                        ? activity.EndUtc
                        : current.EndUtc
                };
                continue;
            }

            merged.Add(current);
            current = activity with { ActivityType = group.Key };
        }

        merged.Add(current);

        return merged;
    }
}

internal sealed record ActivityInterval(
    Guid Id,
    string ActivityType,
    DateTime StartUtc,
    DateTime EndUtc);
