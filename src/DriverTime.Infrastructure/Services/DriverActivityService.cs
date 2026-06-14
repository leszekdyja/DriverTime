using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverActivityService : IDriverActivityService
{
    private readonly ApplicationDbContext _dbContext;

    public DriverActivityService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<DriverActivityDto>> GetActivitiesAsync(
        DateTime? from,
        DateTime? to)
    {
        var query = _dbContext.DriverActivities
            .AsNoTracking()
            .AsQueryable();

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(
                from.Value,
                DateTimeKind.Utc);

            query = query.Where(x => x.StartUtc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(
                to.Value,
                DateTimeKind.Utc);

            query = query.Where(x => x.StartUtc <= toUtc);
        }

        return await query
            .OrderBy(x => x.StartUtc)
            .Select(x => new DriverActivityDto
            {
                Id = x.Id,
                DddFileId = x.DddFileId,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                ActivityType = x.ActivityType,
                DurationSeconds = (int)(x.EndUtc - x.StartUtc).TotalSeconds
            })
            .ToListAsync();
    }
}