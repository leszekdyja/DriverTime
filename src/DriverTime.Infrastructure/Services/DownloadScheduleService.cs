using DriverTime.Application.Downloads;
using DriverTime.Application.Downloads.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DownloadScheduleService : IDownloadScheduleService
{
    private readonly DriverTimeDbContext _dbContext;

    public DownloadScheduleService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DriverDownloadDto>> GetDriverDownloadsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new
            {
                Driver = x,
                LastActivityUtc = x.DddFiles
                    .SelectMany(file => file.DriverActivities)
                    .Select(activity => (DateTime?)activity.EndUtc)
                    .Max()
            })
            .OrderBy(x => x.Driver.LastName)
            .ThenBy(x => x.Driver.FirstName)
            .ToListAsync(cancellationToken);

        return drivers
            .Select(x => new DriverDownloadDto
            {
                DriverId = x.Driver.Id,
                FirstName = x.Driver.FirstName,
                LastName = x.Driver.LastName,
                CardNumber = x.Driver.CardNumber,
                LastDownloadUtc = x.LastActivityUtc,
                NextRequiredDownloadUtc = DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                    x.LastActivityUtc,
                    DownloadScheduleCalculator.DriverDownloadIntervalDays),
                DaysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(
                    DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                        x.LastActivityUtc,
                        DownloadScheduleCalculator.DriverDownloadIntervalDays),
                    nowUtc),
                Status = DownloadScheduleCalculator.GetStatus(
                    DownloadScheduleCalculator.GetDaysUntilDue(
                        DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                            x.LastActivityUtc,
                            DownloadScheduleCalculator.DriverDownloadIntervalDays),
                        nowUtc))
            })
            .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToList();
    }

    public async Task<IReadOnlyList<VehicleDownloadDto>> GetVehicleDownloadsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var vehicles = await _dbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Active)
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => new
            {
                Vehicle = x,
                LastActivityUtc = _dbContext.VehicleUses
                    .Where(vehicleUse =>
                        vehicleUse.DddFile.CompanyId == companyId
                        && vehicleUse.RegistrationNumber != null
                        && vehicleUse.RegistrationNumber.Replace(" ", "").Length >= 5
                        && EF.Functions.Like(
                            x.RegistrationNumber.Replace(" ", "").ToUpper(),
                            "%" + vehicleUse.RegistrationNumber.Replace(" ", "").ToUpper()))
                    .Select(vehicleUse => (DateTime?)vehicleUse.EndUtc)
                    .Max()
            })
            .ToListAsync(cancellationToken);

        return vehicles
            .Select(x => new VehicleDownloadDto
            {
                VehicleId = x.Vehicle.Id,
                RegistrationNumber = x.Vehicle.RegistrationNumber,
                LastDownloadUtc = x.LastActivityUtc,
                NextRequiredDownloadUtc = DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                    x.LastActivityUtc,
                    DownloadScheduleCalculator.VehicleDownloadIntervalDays),
                DaysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(
                    DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                        x.LastActivityUtc,
                        DownloadScheduleCalculator.VehicleDownloadIntervalDays),
                    nowUtc),
                Status = DownloadScheduleCalculator.GetStatus(
                    DownloadScheduleCalculator.GetDaysUntilDue(
                        DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                            x.LastActivityUtc,
                            DownloadScheduleCalculator.VehicleDownloadIntervalDays),
                        nowUtc))
            })
            .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
            .ThenBy(x => x.RegistrationNumber)
            .ToList();
    }

    public async Task<DownloadDashboardDto> GetDashboardAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var drivers = await GetDriverDownloadsAsync(companyId, cancellationToken);
        var vehicles = await GetVehicleDownloadsAsync(companyId, cancellationToken);

        return new DownloadDashboardDto
        {
            OverdueDrivers = drivers.Count(x => x.Status == DownloadStatus.Overdue),
            WarningDrivers = drivers.Count(x => x.Status == DownloadStatus.Warning),
            OverdueVehicles = vehicles.Count(x => x.Status == DownloadStatus.Overdue),
            WarningVehicles = vehicles.Count(x => x.Status == DownloadStatus.Warning),
            NextDriversDue = drivers
                .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
                .Take(5)
                .ToList(),
            NextVehiclesDue = vehicles
                .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
                .Take(5)
                .ToList()
        };
    }

}
