using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverActivityService : IDriverActivityService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverActivityService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<DriverActivityDto>> GetActivitiesAsync(
        DateTime? from,
        DateTime? to,
        string? driverCardNumber)
    {
        var query = _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x => x.DddFile.CompanyId == _currentUser.CompanyId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(driverCardNumber))
        {
            query = query.Where(
                x => x.DddFile.DriverCardNumber == driverCardNumber);
        }

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
                DriverFirstName = x.DddFile.DriverFirstName,
                DriverLastName = x.DddFile.DriverLastName,
                DriverCardNumber = x.DddFile.DriverCardNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                ActivityType = x.ActivityType,
                DurationSeconds = (int)(x.EndUtc - x.StartUtc).TotalSeconds
            })
            .ToListAsync();
    }
}
