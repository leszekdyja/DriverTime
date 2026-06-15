using DriverTime.Application.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly DriverTimeDbContext _dbContext;

    public DashboardService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        return new DashboardDto
        {
            DddFilesCount = await _dbContext.DddFiles.CountAsync(cancellationToken),

            DriverActivitiesCount = await _dbContext.DriverActivities.CountAsync(cancellationToken),

            VehicleUsesCount = await _dbContext.VehicleUses.CountAsync(cancellationToken),

            CountryEntriesCount = await _dbContext.CountryEntries.CountAsync(cancellationToken),

            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
