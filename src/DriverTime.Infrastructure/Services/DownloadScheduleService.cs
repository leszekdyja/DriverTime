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
                x.Id,
                x.RegistrationNumber
            })
            .ToListAsync(cancellationToken);

        var vehicleUses = await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == companyId
                && x.RegistrationNumber != null
                && x.RegistrationNumber.Replace(" ", "").Length >= 5)
            .Select(x => new VehicleUseDownloadSource
            {
                RegistrationNumber = x.RegistrationNumber,
                EndUtc = x.EndUtc
            })
            .ToListAsync(cancellationToken);

        var normalizedVehicleUses = vehicleUses
            .Select(x => new NormalizedVehicleUseDownloadSource
            {
                RegistrationNumber = NormalizeVehicleRegistration(x.RegistrationNumber),
                CompactRegistrationNumber = GetVehicleRegistrationCompactValue(x.RegistrationNumber),
                EndUtc = x.EndUtc
            })
            .Where(x => x.CompactRegistrationNumber.Length >= 5)
            .ToList();

        return vehicles
            .Select(x =>
            {
                var compactRegistrationNumber = GetVehicleRegistrationCompactValue(x.RegistrationNumber);
                var matchingUses = normalizedVehicleUses
                    .Where(vehicleUse => IsSameVehicleRegistration(
                        compactRegistrationNumber,
                        vehicleUse.CompactRegistrationNumber))
                    .ToList();
                var lastActivityUtc = matchingUses
                    .Select(vehicleUse => (DateTime?)vehicleUse.EndUtc)
                    .Max();
                var registrationNumber = GetBestVehicleRegistration(
                    x.RegistrationNumber,
                    matchingUses.Select(vehicleUse => vehicleUse.RegistrationNumber));
                var nextRequiredDownloadUtc = DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
                    lastActivityUtc,
                    DownloadScheduleCalculator.VehicleDownloadIntervalDays);
                var daysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(
                    nextRequiredDownloadUtc,
                    nowUtc);

                return new VehicleDownloadDto
                {
                    VehicleId = x.Id,
                    RegistrationNumber = registrationNumber,
                    LastDownloadUtc = lastActivityUtc,
                    NextRequiredDownloadUtc = nextRequiredDownloadUtc,
                    DaysUntilDue = daysUntilDue,
                    Status = DownloadScheduleCalculator.GetStatus(daysUntilDue)
                };
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

    private static string NormalizeVehicleRegistration(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(
                " ",
                value.Trim()
                    .ToUpperInvariant()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string GetVehicleRegistrationCompactValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value
                .Where(x => !char.IsWhiteSpace(x))
                .ToArray())
                .ToUpperInvariant();
    }

    private static bool IsSameVehicleRegistration(
        string registrationNumber,
        string candidate)
    {
        if (registrationNumber.Length < 5 || candidate.Length < 5)
        {
            return false;
        }

        return string.Equals(registrationNumber, candidate, StringComparison.OrdinalIgnoreCase)
            || registrationNumber.Contains(candidate, StringComparison.OrdinalIgnoreCase)
            || candidate.Contains(registrationNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetBestVehicleRegistration(
        string vehicleRegistrationNumber,
        IEnumerable<string> vehicleUseRegistrationNumbers)
    {
        return vehicleUseRegistrationNumbers
            .Append(vehicleRegistrationNumber)
            .Select(NormalizeVehicleRegistration)
            .Where(x => GetVehicleRegistrationCompactValue(x).Length >= 5)
            .OrderByDescending(x => GetVehicleRegistrationCompactValue(x).Length)
            .ThenBy(x => x)
            .FirstOrDefault() ?? NormalizeVehicleRegistration(vehicleRegistrationNumber);
    }

    private sealed class VehicleUseDownloadSource
    {
        public string RegistrationNumber { get; set; } = string.Empty;

        public DateTime EndUtc { get; set; }
    }

    private sealed class NormalizedVehicleUseDownloadSource
    {
        public string RegistrationNumber { get; set; } = string.Empty;

        public string CompactRegistrationNumber { get; set; } = string.Empty;

        public DateTime EndUtc { get; set; }
    }

}
