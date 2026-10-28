using DriverTime.Application.Downloads;
using DriverTime.Application.Downloads.DTOs;
using DriverTime.Application.DDD.DTOs;
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
        Guid? operatingCompanyId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId
                && (!operatingCompanyId.HasValue || x.OperatingCompanyId == operatingCompanyId.Value))
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(cancellationToken);

        var importedCards = await _dbContext.DddFiles.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DriverId.HasValue
                && x.Driver != null
                && (!operatingCompanyId.HasValue || x.Driver.OperatingCompanyId == operatingCompanyId.Value))
            .Select(x => new { DriverId = x.DriverId!.Value, x.DriverCardNumber,
                x.FileName, x.CardReadAtUtc, x.CardReadDateSource })
            .Distinct().ToListAsync(cancellationToken);
        var confirmedCards = importedCards.Select(x => (x.DriverId, x.DriverCardNumber)).ToHashSet();
        var activeDrivers = drivers.Where(old => !drivers.Any(replacement =>
            IsReplacedExpiredCard(old, replacement, nowUtc)
            && confirmedCards.Contains((replacement.Id, replacement.CardNumber)))).ToList();
        var readDates = importedCards.Select(file => new { File = file,
                Read = CardReadDateResolver.Resolve(file.CardReadAtUtc, file.CardReadDateSource, file.FileName, nowUtc) })
            .Where(x => x.Read is not null)
            .GroupBy(x => (x.File.DriverId, x.File.DriverCardNumber))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.Read!.AtUtc).First().Read!);

        var lastActivities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == companyId
                && (!operatingCompanyId.HasValue
                    || (x.DddFile.Driver != null && x.DddFile.Driver.OperatingCompanyId == operatingCompanyId.Value))
                && x.DddFile.DriverId.HasValue
                && x.DddFile.Driver != null
                && x.DddFile.DriverCardNumber == x.DddFile.Driver.CardNumber)
            .GroupBy(x => x.DddFile.DriverId!.Value)
            .Select(x => new
            {
                DriverId = x.Key,
                LastActivityUtc = (DateTime?)x.Max(activity => activity.EndUtc)
            })
            .ToDictionaryAsync(x => x.DriverId, x => x.LastActivityUtc, cancellationToken);

        return activeDrivers
            .Select(x =>
            {
                var read = readDates.GetValueOrDefault((x.Id, x.CardNumber));
                var lastRead = read?.AtUtc ?? lastActivities.GetValueOrDefault(x.Id);
                var nextRead = DownloadScheduleCalculator.GetNextRequiredDownloadUtc(lastRead,
                    DownloadScheduleCalculator.DriverDownloadIntervalDays);
                var days = DownloadScheduleCalculator.GetDaysUntilDue(nextRead, nowUtc);
                return new DriverDownloadDto
                {
                    DriverId = x.Id,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    CardNumber = x.CardNumber,
                    LastDownloadUtc = lastRead,
                    LastDownloadSource = read?.Source ?? (lastRead.HasValue ? "ActivityEstimate" : "Unknown"),
                    NextRequiredDownloadUtc = nextRead,
                    DaysUntilDue = days,
                    Status = DownloadScheduleCalculator.GetStatus(days)
                };
            })
            .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToList();
    }

    internal static bool IsReplacedExpiredCard(Driver old, Driver replacement, DateTime nowUtc)
    {
        if (old.Id == replacement.Id || old.CompanyId != replacement.CompanyId
            || old.OperatingCompanyId != replacement.OperatingCompanyId
            || !old.CardExpiryDate.HasValue || old.CardExpiryDate.Value.Date >= nowUtc.Date
            || !replacement.CardExpiryDate.HasValue || replacement.CardExpiryDate.Value.Date < nowUtc.Date
            || replacement.CardExpiryDate <= old.CardExpiryDate || old.CardNumber == replacement.CardNumber)
            return false;
        var identity = DddFileService.GetCardIdentity(old.CardNumber);
        return identity is not null && identity == DddFileService.GetCardIdentity(replacement.CardNumber)
            && !string.IsNullOrWhiteSpace(old.FirstName) && !string.IsNullOrWhiteSpace(old.LastName)
            && DddFileService.IsSameDriverPerson(old, new ParsedDriverDto {
                FirstName = replacement.FirstName, LastName = replacement.LastName,
                CardIssuingCountry = replacement.CardIssuingCountry
            });
    }

    public async Task<IReadOnlyList<VehicleDownloadDto>> GetVehicleDownloadsAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        CancellationToken cancellationToken = default)
    {
        var vehicles = await _dbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Active
                && (!operatingCompanyId.HasValue || x.OperatingCompanyId == operatingCompanyId.Value))
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => new
            {
                x.Id,
                x.RegistrationNumber
            })
            .ToListAsync(cancellationToken);

        var vehicleRegistrations = await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == companyId
                && (!operatingCompanyId.HasValue
                    || (x.DddFile.Driver != null && x.DddFile.Driver.OperatingCompanyId == operatingCompanyId.Value))
                && x.RegistrationNumber != null
                && x.RegistrationNumber.Replace(" ", "").Length >= 5)
            .Select(x => x.RegistrationNumber)
            .Distinct()
            .ToListAsync(cancellationToken);

        var normalizedVehicleUses = vehicleRegistrations
            .Select(registrationNumber => new NormalizedVehicleUseDownloadSource
            {
                RegistrationNumber = NormalizeVehicleRegistration(registrationNumber),
                CompactRegistrationNumber = GetVehicleRegistrationCompactValue(registrationNumber)
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
                var registrationNumber = GetBestVehicleRegistration(
                    x.RegistrationNumber,
                    matchingUses.Select(vehicleUse => vehicleUse.RegistrationNumber));

                return new VehicleDownloadDto
                {
                    VehicleId = x.Id,
                    RegistrationNumber = registrationNumber,
                    LastDownloadUtc = null,
                    NextRequiredDownloadUtc = null,
                    DaysUntilDue = null,
                    Status = DownloadStatus.NoTachographData
                };
            })
            .OrderBy(x => x.DaysUntilDue ?? int.MinValue)
            .ThenBy(x => x.RegistrationNumber)
            .ToList();
    }

    public async Task<DownloadDashboardDto> GetDashboardAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        CancellationToken cancellationToken = default)
    {
        var drivers = await GetDriverDownloadsAsync(companyId, operatingCompanyId, cancellationToken);
        var vehicles = await GetVehicleDownloadsAsync(companyId, operatingCompanyId, cancellationToken);

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
                .Where(x => x.Status != DownloadStatus.NoTachographData)
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

    private sealed class NormalizedVehicleUseDownloadSource
    {
        public string RegistrationNumber { get; set; } = string.Empty;

        public string CompactRegistrationNumber { get; set; } = string.Empty;
    }

}
