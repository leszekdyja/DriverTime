using DriverTime.Application.Downloads.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/downloads")]
public class DownloadsController : ControllerBase
{
    private const int DriverDownloadIntervalDays = 28;
    private const int VehicleDownloadIntervalDays = 90;
    private const int WarningDays = 7;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DownloadsController(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<IReadOnlyList<DriverDownloadDto>>> GetDrivers(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var drivers = await BuildDriverDownloadsAsync(cancellationToken);

        return Ok(drivers);
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDownloadDto>>> GetVehicles(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var vehicles = await BuildVehicleDownloadsAsync(cancellationToken);

        return Ok(vehicles);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DownloadDashboardDto>> GetDashboard(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var drivers = await BuildDriverDownloadsAsync(cancellationToken);
        var vehicles = await BuildVehicleDownloadsAsync(cancellationToken);

        return Ok(new DownloadDashboardDto
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
        });
    }

    private bool HasCompanyContext()
    {
        return _currentUser.IsAuthenticated && _currentUser.CompanyId != Guid.Empty;
    }

    private async Task<List<DriverDownloadDto>> BuildDriverDownloadsAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .Select(x => new
            {
                Driver = x,
                LastDownloadUtc = x.DddFiles
                    .OrderByDescending(file => file.UploadedAtUtc)
                    .Select(file => (DateTime?)file.UploadedAtUtc)
                    .FirstOrDefault()
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
                LastDownloadUtc = x.LastDownloadUtc,
                NextRequiredDownloadUtc = x.LastDownloadUtc?.AddDays(DriverDownloadIntervalDays),
                DaysUntilDue = GetDaysUntilDue(x.LastDownloadUtc, DriverDownloadIntervalDays, nowUtc),
                Status = GetStatus(x.LastDownloadUtc, DriverDownloadIntervalDays, nowUtc)
            })
            .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToList();
    }

    private async Task<List<VehicleDownloadDto>> BuildVehicleDownloadsAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var vehicles = await _dbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.Active)
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => new
            {
                Vehicle = x,
                LastDownloadUtc = _dbContext.VehicleUses
                    .Where(vehicleUse =>
                        vehicleUse.DddFile.CompanyId == _currentUser.CompanyId
                        && vehicleUse.RegistrationNumber != null
                        && vehicleUse.RegistrationNumber.Replace(" ", "").Length >= 5
                        && EF.Functions.Like(
                            x.RegistrationNumber.Replace(" ", "").ToUpper(),
                            "%" + vehicleUse.RegistrationNumber.Replace(" ", "").ToUpper()))
                    .OrderByDescending(vehicleUse => vehicleUse.DddFile.UploadedAtUtc)
                    .Select(vehicleUse => (DateTime?)vehicleUse.DddFile.UploadedAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return vehicles
            .Select(x => new VehicleDownloadDto
            {
                VehicleId = x.Vehicle.Id,
                RegistrationNumber = x.Vehicle.RegistrationNumber,
                LastDownloadUtc = x.LastDownloadUtc,
                NextRequiredDownloadUtc = x.LastDownloadUtc?.AddDays(VehicleDownloadIntervalDays),
                DaysUntilDue = GetDaysUntilDue(x.LastDownloadUtc, VehicleDownloadIntervalDays, nowUtc),
                Status = GetStatus(x.LastDownloadUtc, VehicleDownloadIntervalDays, nowUtc)
            })
            .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
            .ThenBy(x => x.RegistrationNumber)
            .ToList();
    }

    private static int? GetDaysUntilDue(
        DateTime? lastDownloadUtc,
        int intervalDays,
        DateTime nowUtc)
    {
        if (!lastDownloadUtc.HasValue)
        {
            return null;
        }

        var nextRequiredDownloadUtc = lastDownloadUtc.Value.AddDays(intervalDays);

        return (int)Math.Floor((nextRequiredDownloadUtc.Date - nowUtc.Date).TotalDays);
    }

    private static string GetStatus(
        DateTime? lastDownloadUtc,
        int intervalDays,
        DateTime nowUtc)
    {
        var daysUntilDue = GetDaysUntilDue(lastDownloadUtc, intervalDays, nowUtc);

        if (!daysUntilDue.HasValue || daysUntilDue.Value < 0)
        {
            return DownloadStatus.Overdue;
        }

        return daysUntilDue.Value <= WarningDays
            ? DownloadStatus.Warning
            : DownloadStatus.Ok;
    }
}
