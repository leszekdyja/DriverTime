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

        var rawActivities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == companyId &&
                x.DddFile.DriverId == driverId)
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
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var timeline = new List<TimelineActivity>();

        foreach (var activity in ordered)
        {
            var normalized = new TimelineActivity
            {
                SourceActivityId = activity.SourceActivityId,
                DriverId = activity.DriverId,
                ActivityType = activity.ActivityType,
                StartUtc = activity.StartUtc,
                EndUtc = activity.EndUtc
            };

            if (timeline.Count > 0)
            {
                var previous = timeline[^1];

                if (normalized.StartUtc < previous.EndUtc)
                {
                    normalized.StartUtc = previous.EndUtc;
                }

                if (normalized.StartUtc >= normalized.EndUtc)
                {
                    continue;
                }

                if (previous.ActivityType == normalized.ActivityType &&
                    previous.EndUtc == normalized.StartUtc)
                {
                    previous.EndUtc = normalized.EndUtc;
                    continue;
                }
            }

            timeline.Add(normalized);
        }

        return timeline;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}