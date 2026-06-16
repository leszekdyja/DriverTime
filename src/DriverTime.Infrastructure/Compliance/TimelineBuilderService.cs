using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Compliance;

public class TimelineBuilderService : ITimelineBuilderService
{
    private readonly DriverTimeDbContext _dbContext;

    public TimelineBuilderService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
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
            .Include(x => x.DddFile)
            .Where(x =>
                x.DddFile.CompanyId == companyId &&
                x.DddFile.DriverId == driverId)
            .OrderBy(x => x.StartUtc)
            .Select(x => new TimelineActivity
            {
                SourceActivityId = x.Id,
                DriverId = driverId,
                ActivityType = NormalizeActivityType(x.ActivityType),
                StartUtc = EnsureUtc(x.StartUtc),
                EndUtc = EnsureUtc(x.EndUtc)
            })
            .ToListAsync(cancellationToken);

        return NormalizeTimeline(rawActivities);
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

    private static string NormalizeActivityType(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();

        return normalized switch
        {
            "JAZDA" => "DRIVING",
            "PRACA" => "WORK",
            "ODPOCZYNEK" => "REST",
            "BREAK" => "REST",
            "DYSPOZYCJA" => "AVAILABILITY",
            _ => normalized
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
