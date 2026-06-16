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

        LogActivityTypeMetrics(driverId, normalizedActivities, unknownActivities.Count);

        return NormalizeTimeline(normalizedActivities);
    }

    private void LogActivityTypeMetrics(
        Guid driverId,
        IReadOnlyCollection<TimelineActivity> activities,
        int unknownCount)
    {
        _logger.LogInformation(
            "Compliance timeline activity counts for driver {DriverId}: DRIVING={DrivingCount}, WORK={WorkCount}, REST={RestCount}, AVAILABILITY={AvailabilityCount}, UNKNOWN={UnknownCount}",
            driverId,
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Driving),
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Work),
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Rest),
            activities.Count(x => x.ActivityType == ActivityTypeNormalizer.Availability),
            unknownCount);
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
