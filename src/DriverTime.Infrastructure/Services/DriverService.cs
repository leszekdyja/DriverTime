using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Services;

public class DriverService : IDriverService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDriverViolationService _driverViolationService;
    private readonly ILogger<DriverService> _logger;

    public DriverService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IDriverViolationService driverViolationService,
        ILogger<DriverService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _driverViolationService = driverViolationService;
        _logger = logger;
    }

    public async Task<List<DriverDto>> GetAllAsync()
    {
        var isAuthenticated = _currentUser.IsAuthenticated;
        var userId = _currentUser.UserId;
        var companyId = _currentUser.CompanyId;
        var allDriversCount = await _dbContext.Drivers.CountAsync();
        var filteredDriversCount = await _dbContext.Drivers
            .CountAsync(x => x.CompanyId == companyId);
        var driverCompanyIds = await _dbContext.Drivers
            .Select(x => x.CompanyId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        _logger.LogInformation(
            "GET /api/drivers diagnostics: IsAuthenticated={IsAuthenticated}, UserId={UserId}, CurrentUserCompanyId={CompanyId}, DriversBeforeFilter={DriversBeforeFilter}, DriversAfterCompanyFilter={DriversAfterCompanyFilter}, DriverCompanyIds={DriverCompanyIds}.",
            isAuthenticated,
            userId,
            companyId,
            allDriversCount,
            filteredDriversCount,
            string.Join(", ", driverCompanyIds));

        return await _dbContext.Drivers
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => new DriverDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CardNumber = x.CardNumber,
                CardExpiryDate = x.CardExpiryDate,
                CardIssuingCountry = x.CardIssuingCountry,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DriverDetailsDto?> GetByIdAsync(Guid id)
    {
        var driver = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId)
            .Select(x => new DriverDetailsDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CardNumber = x.CardNumber,
                CardExpiryDate = x.CardExpiryDate,
                CardIssuingCountry = x.CardIssuingCountry,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .FirstOrDefaultAsync();

        if (driver is null)
        {
            return null;
        }

        var imports = await _dbContext.DddFiles
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == _currentUser.CompanyId
                && x.DriverId == id)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new DriverImportDto
            {
                Id = x.Id,
                FileName = x.FileName,
                UploadedAtUtc = x.UploadedAtUtc,
                ActivitiesCount = x.DriverActivities.Count
            })
            .ToListAsync();

        var activities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.DddFile.DriverId == id)
            .OrderByDescending(x => x.StartUtc)
            .Select(x => new DriverDetailsActivityDto
            {
                Id = x.Id,
                ActivityType = x.ActivityType,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc
            })
            .ToListAsync();

        foreach (var activity in activities)
        {
            activity.DurationSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                activity.StartUtc,
                activity.EndUtc);
        }

        var mergedActivities = ActivityIntervalAggregationHelper.ClipAndMergeByType(
            activities.Select(activity => new ActivityInterval(
                activity.Id,
                activity.ActivityType,
                activity.StartUtc,
                activity.EndUtc)));

        foreach (var activity in mergedActivities)
        {
            AddDuration(
                driver,
                activity.ActivityType,
                ActivityIntervalAggregationHelper.GetDurationSeconds(
                    activity.StartUtc,
                    activity.EndUtc));
        }

        driver.ImportsCount = imports.Count;
        driver.LastImportAtUtc = imports.FirstOrDefault()?.UploadedAtUtc;
        driver.RecentImports = imports.Take(10).ToList();
        driver.RecentActivities = activities.Take(20).ToList();

        var visibleTimelineRange = GetVisibleTimelineRange(driver.RecentActivities);
        if (visibleTimelineRange is not null)
        {
            driver.CountryEntries = await GetCountryEntriesAsync(
                id,
                visibleTimelineRange.Value.StartUtc,
                visibleTimelineRange.Value.EndUtc);
            driver.VehicleUses = await GetVehicleUsesAsync(
                id,
                visibleTimelineRange.Value.StartUtc,
                visibleTimelineRange.Value.EndUtc);
        }

        driver.RecentViolations = ((await _driverViolationService
                .GetViolationsForDriverAsync(id)) ?? Array.Empty<DriverViolationDto>())
            .Take(10)
            .ToList();
        driver.Vehicles = await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.DddFile.DriverId == id
                && x.RegistrationNumber != string.Empty)
            .GroupBy(x => x.RegistrationNumber)
            .Select(group => new DriverVehicleDto
            {
                RegistrationNumber = group.Key,
                FirstUsedAtUtc = group.Min(x => x.StartUtc),
                LastUsedAtUtc = group.Max(x => x.EndUtc),
                UsageCount = group.Count()
            })
            .OrderByDescending(x => x.LastUsedAtUtc)
            .ToListAsync();

        return driver;
    }


    private async Task<List<DriverCountryEntryDto>> GetCountryEntriesAsync(
        Guid driverId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc)
    {
        return await _dbContext.CountryEntries
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.DddFile.DriverId == driverId
                && x.EntryTimeUtc >= rangeStartUtc
                && x.EntryTimeUtc < rangeEndUtc)
            .OrderBy(x => x.EntryTimeUtc)
            .Select(x => new DriverCountryEntryDto
            {
                Id = x.Id,
                EntryTimeUtc = x.EntryTimeUtc,
                CountryCode = x.CountryCode,
                EntryType = "Unknown"
            })
            .ToListAsync();
    }

    private async Task<List<DriverVehicleUseDto>> GetVehicleUsesAsync(
        Guid driverId,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc)
    {
        return await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.DddFile.DriverId == driverId
                && x.RegistrationNumber != string.Empty
                && x.StartUtc < rangeEndUtc
                && x.EndUtc > rangeStartUtc)
            .OrderBy(x => x.StartUtc)
            .Select(x => new DriverVehicleUseDto
            {
                Id = x.Id,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                RegistrationNumber = x.RegistrationNumber,
                DistanceKm = x.DistanceKm,
                StartOdometerKm = x.StartOdometerKm,
                EndOdometerKm = x.EndOdometerKm
            })
            .ToListAsync();
    }

    private static (DateTime StartUtc, DateTime EndUtc)? GetVisibleTimelineRange(
        IReadOnlyCollection<DriverDetailsActivityDto> activities)
    {
        if (activities.Count == 0)
        {
            return null;
        }

        var firstStartUtc = AsUtc(activities.Min(x => x.StartUtc));
        var lastEndUtc = AsUtc(activities.Max(x => x.EndUtc));
        var rangeStartUtc = firstStartUtc.Date;
        var rangeEndUtc = lastEndUtc.Date.AddDays(1);

        return (rangeStartUtc, rangeEndUtc);
    }

    private static DateTime AsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
    private static void AddDuration(
        DriverDetailsDto driver,
        string activityType,
        long durationSeconds)
    {
        switch (activityType.ToUpperInvariant())
        {
            case "DRIVING":
                driver.DrivingSeconds += durationSeconds;
                break;
            case "WORK":
                driver.WorkSeconds += durationSeconds;
                break;
            case "REST":
                driver.RestSeconds += durationSeconds;
                break;
            case "AVAILABILITY":
                driver.AvailabilitySeconds += durationSeconds;
                break;
        }
    }

    public async Task<DriverDto> CreateAsync(CreateDriverDto dto)
    {
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CardNumber = dto.CardNumber,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Drivers.Add(driver);

        await _dbContext.SaveChangesAsync();

        return new DriverDto
        {
            Id = driver.Id,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            CardNumber = driver.CardNumber,
            CardExpiryDate = driver.CardExpiryDate,
            CardIssuingCountry = driver.CardIssuingCountry,
            CreatedAtUtc = driver.CreatedAtUtc
        };
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == id && x.CompanyId == companyId,
                cancellationToken);

        if (!driverExists)
        {
            return false;
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var dddFileIds = await _dbContext.DddFiles
            .Where(x => x.CompanyId == companyId && x.DriverId == id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await _dbContext.ComplianceRunViolations
            .Where(x =>
                x.ComplianceRun.CompanyId == companyId &&
                x.ComplianceRun.DriverId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.ComplianceRuns
            .Where(x => x.CompanyId == companyId && x.DriverId == id)
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.Violations
            .Where(x =>
                x.DriverId == id &&
                x.Driver != null &&
                x.Driver.CompanyId == companyId)
            .ExecuteDeleteAsync(cancellationToken);

        if (dddFileIds.Count > 0)
        {
            await _dbContext.DriverActivities
                .Where(x => dddFileIds.Contains(x.DddFileId))
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.VehicleUses
                .Where(x => dddFileIds.Contains(x.DddFileId))
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.CountryEntries
                .Where(x => dddFileIds.Contains(x.DddFileId))
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.DddFiles
                .Where(x => dddFileIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var deletedDrivers = await _dbContext.Drivers
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return deletedDrivers > 0;
    }

    internal static bool IsDriverInCompanyScope(
        Driver driver,
        Guid driverId,
        Guid companyId)
    {
        return driver.Id == driverId && driver.CompanyId == companyId;
    }

    internal static bool IsDddFileInDriverDeletionScope(
        DddFile dddFile,
        Guid driverId,
        Guid companyId)
    {
        return dddFile.CompanyId == companyId && dddFile.DriverId == driverId;
    }

    internal static bool IsViolationInDriverDeletionScope(
        Violation violation,
        Guid driverId,
        Guid companyId)
    {
        return violation.DriverId == driverId &&
            violation.Driver?.CompanyId == companyId;
    }

    internal static bool IsComplianceRunInDriverDeletionScope(
        ComplianceRun complianceRun,
        Guid driverId,
        Guid companyId)
    {
        return complianceRun.CompanyId == companyId &&
            complianceRun.DriverId == driverId;
    }
}
