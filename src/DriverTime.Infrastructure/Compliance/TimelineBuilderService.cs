using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance;

public class TimelineBuilderService : ITimelineBuilderService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ILogger<TimelineBuilderService> _logger;

    public TimelineBuilderService(
        DriverTimeDbContext dbContext,
        ILogger<TimelineBuilderService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TimelineActivity>?> BuildForDriverAsync(
        Guid companyId,
        Guid driverId,
        DateTime? queryStartUtc = null,
        DateTime? queryEndUtc = null,
        CancellationToken cancellationToken = default)
    {
        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == driverId && x.CompanyId == companyId,
                cancellationToken);

        if (!driverExists)
        {
            return null;
        }

        var activitiesQuery = _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == companyId &&
                x.DddFile.DriverId == driverId);

        if (queryStartUtc.HasValue)
        {
            var startUtc = EnsureUtc(queryStartUtc.Value);
            activitiesQuery = activitiesQuery.Where(x => x.EndUtc >= startUtc);
        }

        if (queryEndUtc.HasValue)
        {
            var endUtc = EnsureUtc(queryEndUtc.Value);
            activitiesQuery = activitiesQuery.Where(x => x.StartUtc <= endUtc);
        }

        var rawActivities = await activitiesQuery
            .OrderBy(x => x.StartUtc)
            .Select(x => new
            {
                x.Id,
                x.ActivityType,
                x.StartUtc,
                x.EndUtc
            })
            .ToListAsync(cancellationToken);

        var normalizedActivities = new List<TimelineActivity>();
        var unknownActivities = new List<string>();

        foreach (var activity in rawActivities)
        {
            var normalizedType = ActivityTypeNormalizer.Normalize(activity.ActivityType);

            if (normalizedType == ActivityTypeNormalizer.Unknown)
            {
                unknownActivities.Add(activity.ActivityType);
                continue;
            }

            normalizedActivities.Add(new TimelineActivity
            {
                SourceActivityId = activity.Id,
                DriverId = driverId,
                ActivityType = normalizedType,
                StartUtc = EnsureUtc(activity.StartUtc),
                EndUtc = EnsureUtc(activity.EndUtc)
            });
        }

        if (unknownActivities.Count > 0)
        {
            _logger.LogWarning(
                "Compliance timeline ignored {Count} activities with unknown activity type for driver {DriverId}. Examples: {Examples}",
                unknownActivities.Count,
                driverId,
                string.Join(", ", unknownActivities.Distinct().Take(10)));
        }

        var timeline = NormalizeTimeline(normalizedActivities);

        LogActivityTypeMetrics(
            driverId,
            rawActivities.Count,
            normalizedActivities,
            timeline,
            unknownActivities.Count);

        return timeline;
    }

    private void LogActivityTypeMetrics(
        Guid driverId,
        int rawCount,
        IReadOnlyCollection<TimelineActivity> activities,
        IReadOnlyCollection<TimelineActivity> timeline,
        int unknownCount)
    {
        var firstStart = timeline.Count == 0
            ? (DateTime?)null
            : timeline.Min(x => x.StartUtc);

        var lastEnd = timeline.Count == 0
            ? (DateTime?)null
            : timeline.Max(x => x.EndUtc);

        var normalizedDrivingMinutes = activities
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Driving)
            .Sum(x => (int)Math.Round((x.EndUtc - x.StartUtc).TotalMinutes));

        var normalizedWorkMinutes = activities
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Work)
            .Sum(x => (int)Math.Round((x.EndUtc - x.StartUtc).TotalMinutes));

        var normalizedRestMinutes = activities
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Rest)
            .Sum(x => (int)Math.Round((x.EndUtc - x.StartUtc).TotalMinutes));

        var timelineDrivingMinutes = timeline
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Driving)
            .Sum(x => (int)Math.Round((x.EndUtc - x.StartUtc).TotalMinutes));

        var timelineWorkMinutes = timeline
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Work)
            .Sum(x => (int)Math.Round((x.EndUtc - x.StartUtc).TotalMinutes));

        var timelineRestMinutes = timeline
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Rest)
            .Sum(x => (int)Math.Round((x.EndUtc - x.StartUtc).TotalMinutes));

        _logger.LogInformation(
            "Compliance timeline activity counts for driver {DriverId}: RAW={RawCount}, NORMALIZED={NormalizedCount}, TIMELINE={TimelineCount}, DRIVING={DrivingCount}, WORK={WorkCount}, REST={RestCount}, AVAILABILITY={AvailabilityCount}, UNKNOWN={UnknownCount}, FirstStart={FirstStart:o}, LastEnd={LastEnd:o}. Minutes normalized: DRIVING={NormalizedDrivingMinutes}, WORK={NormalizedWorkMinutes}, REST={NormalizedRestMinutes}. Minutes timeline: DRIVING={TimelineDrivingMinutes}, WORK={TimelineWorkMinutes}, REST={TimelineRestMinutes}.",
            driverId,
            rawCount,
            activities.Count,
            timeline.Count,
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Driving),
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Work),
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Rest),
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Availability),
            unknownCount,
            firstStart,
            lastEnd,
            normalizedDrivingMinutes,
            normalizedWorkMinutes,
            normalizedRestMinutes,
            timelineDrivingMinutes,
            timelineWorkMinutes,
            timelineRestMinutes);
    }

    private static IReadOnlyList<TimelineActivity> NormalizeTimeline(
        IEnumerable<TimelineActivity> rawActivities)
    {
        var ordered = rawActivities
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderByDescending(x => GetActivityPriority(x.ActivityType))
            .ThenBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var resolved = new List<TimelineActivity>();

        foreach (var activity in ordered)
        {
            AddResolvedActivity(resolved, new TimelineActivity
            {
                SourceActivityId = activity.SourceActivityId,
                DriverId = activity.DriverId,
                ActivityType = activity.ActivityType,
                StartUtc = activity.StartUtc,
                EndUtc = activity.EndUtc
            });
        }

        return MergeAdjacentSameType(resolved);
    }

    private static void AddResolvedActivity(
        List<TimelineActivity> resolved,
        TimelineActivity activity)
    {
        var remaining = new List<TimelineActivity> { activity };

        foreach (var existing in resolved.OrderBy(x => x.StartUtc).ToList())
        {
            if (remaining.Count == 0)
            {
                break;
            }

            if (existing.EndUtc <= activity.StartUtc)
            {
                continue;
            }

            if (existing.StartUtc >= activity.EndUtc)
            {
                break;
            }

            var next = new List<TimelineActivity>();

            foreach (var segment in remaining)
            {
                if (existing.EndUtc <= segment.StartUtc ||
                    existing.StartUtc >= segment.EndUtc)
                {
                    next.Add(segment);
                    continue;
                }

                var existingPriority = GetActivityPriority(existing.ActivityType);
                var segmentPriority = GetActivityPriority(segment.ActivityType);

                if (existingPriority >= segmentPriority)
                {
                    next.AddRange(SubtractOverlap(segment, existing));
                    continue;
                }

                resolved.Remove(existing);
                resolved.AddRange(SubtractOverlap(existing, segment));
                next.Add(segment);
            }

            remaining = next;
        }

        foreach (var segment in remaining.Where(x => x.StartUtc < x.EndUtc))
        {
            resolved.Add(segment);
        }
    }

    private static IReadOnlyList<TimelineActivity> MergeAdjacentSameType(
        IEnumerable<TimelineActivity> activities)
    {
        var ordered = activities
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return Array.Empty<TimelineActivity>();
        }

        var merged = new List<TimelineActivity>();

        foreach (var activity in ordered)
        {
            if (merged.Count > 0)
            {
                var previous = merged[^1];

                if (previous.ActivityType == activity.ActivityType &&
                    previous.EndUtc == activity.StartUtc)
                {
                    previous.EndUtc = activity.EndUtc;
                    continue;
                }
            }

            merged.Add(new TimelineActivity
            {
                SourceActivityId = activity.SourceActivityId,
                DriverId = activity.DriverId,
                ActivityType = activity.ActivityType,
                StartUtc = activity.StartUtc,
                EndUtc = activity.EndUtc
            });
        }

        return merged;
    }

    private static IEnumerable<TimelineActivity> SubtractOverlap(
        TimelineActivity activity,
        TimelineActivity overlap)
    {
        if (overlap.StartUtc > activity.StartUtc)
        {
            yield return new TimelineActivity
            {
                SourceActivityId = activity.SourceActivityId,
                DriverId = activity.DriverId,
                ActivityType = activity.ActivityType,
                StartUtc = activity.StartUtc,
                EndUtc = overlap.StartUtc
            };
        }

        if (overlap.EndUtc < activity.EndUtc)
        {
            yield return new TimelineActivity
            {
                SourceActivityId = activity.SourceActivityId,
                DriverId = activity.DriverId,
                ActivityType = activity.ActivityType,
                StartUtc = overlap.EndUtc,
                EndUtc = activity.EndUtc
            };
        }
    }

    private static int GetActivityPriority(string activityType)
    {
        return activityType switch
        {
            ActivityTypeNormalizer.Rest => 50,
            ActivityTypeNormalizer.Availability => 40,
            ActivityTypeNormalizer.Work => 30,
            ActivityTypeNormalizer.Driving => 10,
            _ => 0
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
