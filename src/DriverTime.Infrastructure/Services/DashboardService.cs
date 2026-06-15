using DriverTime.Application.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        return new DashboardDto
        {
            DddFilesCount = await _dbContext.DddFiles
                .CountAsync(x => x.CompanyId == _currentUser.CompanyId, cancellationToken),

            DriverActivitiesCount = await _dbContext.DriverActivities
                .CountAsync(x => x.DddFile.CompanyId == _currentUser.CompanyId, cancellationToken),

            VehicleUsesCount = await _dbContext.VehicleUses
                .CountAsync(x => x.DddFile.CompanyId == _currentUser.CompanyId, cancellationToken),

            CountryEntriesCount = await _dbContext.CountryEntries
                .CountAsync(x => x.DddFile.CompanyId == _currentUser.CompanyId, cancellationToken),

            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
