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
    private readonly ILogger<DriverService> _logger;

    public DriverService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<DriverService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
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

        var query = _dbContext.Drivers
            .Where(x => x.CompanyId == _currentUser.CompanyId);

        if (_currentUser.OperatingCompanyId.HasValue)
        {
            query = query.Where(x => x.OperatingCompanyId == _currentUser.OperatingCompanyId.Value);
        }

        if (_currentUser.IsMobileDriver)
        {
            query = query.Where(x => x.Id == _currentUser.DriverId);
        }

        return await query
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
                OperatingCompanyId = x.OperatingCompanyId,
                OperatingCompanyName = x.OperatingCompany != null ? x.OperatingCompany.Name : null,
                IncludeInPlanning = x.IncludeInPlanning,
                IsActive = x.IsActive,
                PlanningNoNightDuty = x.PlanningNoNightDuty,
                PlanningNoWeekends = x.PlanningNoWeekends,
                PlanningNoSaturdays = x.PlanningNoSaturdays,
                PlanningNoHolidays = x.PlanningNoHolidays,
                PlanningNoDaysOff = x.PlanningNoDaysOff,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DriverDetailsDto?> GetByIdAsync(Guid id)
    {
        if (_currentUser.IsMobileDriver && id != _currentUser.DriverId)
        {
            return null;
        }

        var driver = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId
                && (!_currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == _currentUser.OperatingCompanyId.Value))
            .Select(x => new DriverDetailsDto
            {
                Id = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CardNumber = x.CardNumber,
                CardExpiryDate = x.CardExpiryDate,
                CardIssuingCountry = x.CardIssuingCountry,
                OperatingCompanyId = x.OperatingCompanyId,
                OperatingCompanyName = x.OperatingCompany != null ? x.OperatingCompany.Name : null,
                IncludeInPlanning = x.IncludeInPlanning,
                IsActive = x.IsActive,
                PlanningNoNightDuty = x.PlanningNoNightDuty,
                PlanningNoWeekends = x.PlanningNoWeekends,
                PlanningNoSaturdays = x.PlanningNoSaturdays,
                PlanningNoHolidays = x.PlanningNoHolidays,
                PlanningNoDaysOff = x.PlanningNoDaysOff,
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
                IsCrew = x.IsCrew,
                CardSlot = x.CardSlot,
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

        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-ViolationQueryService.DisplayRangeDays);
        var recentViolations = await _dbContext.Violations
            .AsNoTracking()
            .Where(x => x.DriverId == id && x.ViolationEnd >= cutoff && x.ViolationStart <= now)
            .OrderByDescending(x => x.ViolationStart)
            .ThenBy(x => x.Id)
            .Take(10)
            .ToListAsync();
        driver.RecentViolations = recentViolations.Select(x => new DriverViolationDto
        {
            Code = x.RegulationReference,
            DriverFirstName = driver.FirstName,
            DriverLastName = driver.LastName,
            DriverCardNumber = driver.CardNumber,
            ViolationType = x.ViolationType,
            OccurredAtUtc = x.ViolationStart,
            PeriodEndUtc = x.ViolationEnd,
            Description = ViolationQueryService.BuildDescription(x),
            Severity = x.Severity,
            ActualDurationMinutes = x.DurationMinutes,
            LimitDurationMinutes = ViolationQueryService.GetLimitMinutes(x.RegulationReference)
        }).ToList();

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

        var vehicleOwners = await _dbContext.Vehicles
            .AsNoTracking()
            .Include(x => x.OperatingCompany)
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .Select(x => new
            {
                x.RegistrationNumber,
                x.OperatingCompanyId,
                OperatingCompanyName = x.OperatingCompany == null ? null : x.OperatingCompany.Name
            })
            .ToListAsync();
        var ownersByRegistration = vehicleOwners
            .GroupBy(x => NormalizeVehicleRegistration(x.RegistrationNumber))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var vehicle in driver.Vehicles)
        {
            if (!ownersByRegistration.TryGetValue(NormalizeVehicleRegistration(vehicle.RegistrationNumber), out var owner))
            {
                continue;
            }

            vehicle.OperatingCompanyId = owner.OperatingCompanyId;
            vehicle.OperatingCompanyName = owner.OperatingCompanyName;
            vehicle.IsOutsideDriverCompany = owner.OperatingCompanyId.HasValue
                && driver.OperatingCompanyId.HasValue
                && owner.OperatingCompanyId != driver.OperatingCompanyId;
        }

        return driver;
    }

    private static string NormalizeVehicleRegistration(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Trim().ToUpperInvariant();


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
                EntryType = x.EntryType
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
            OperatingCompanyId = _currentUser.OperatingCompanyId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CardNumber = dto.CardNumber,
            IncludeInPlanning = dto.IncludeInPlanning,
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
            IncludeInPlanning = driver.IncludeInPlanning,
            IsActive = driver.IsActive,
            CreatedAtUtc = driver.CreatedAtUtc
        };
    }


    public async Task<DriverDto?> UpdatePlanningAsync(
        Guid id,
        UpdateDriverPlanningDto dto,
        CancellationToken cancellationToken = default)
    {
        var driver = await _dbContext.Drivers
            .Include(x => x.OperatingCompany)
            .FirstOrDefaultAsync(
                x => x.Id == id && x.CompanyId == _currentUser.CompanyId
                    && (!_currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == _currentUser.OperatingCompanyId.Value),
                cancellationToken);

        if (driver is null)
        {
            return null;
        }

        driver.IncludeInPlanning = dto.IncludeInPlanning;
        if (dto.IsActive.HasValue) driver.IsActive = dto.IsActive.Value;
        driver.PlanningNoNightDuty = dto.PlanningNoNightDuty;
        driver.PlanningNoWeekends = dto.PlanningNoWeekends;
        driver.PlanningNoSaturdays = dto.PlanningNoSaturdays;
        driver.PlanningNoHolidays = dto.PlanningNoHolidays;
        driver.PlanningNoDaysOff = dto.PlanningNoDaysOff;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DriverDto
        {
            Id = driver.Id,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            CardNumber = driver.CardNumber,
            CardExpiryDate = driver.CardExpiryDate,
            CardIssuingCountry = driver.CardIssuingCountry,
            OperatingCompanyId = driver.OperatingCompanyId,
            OperatingCompanyName = driver.OperatingCompany?.Name,
            IncludeInPlanning = driver.IncludeInPlanning,
            IsActive = driver.IsActive,
            PlanningNoNightDuty = driver.PlanningNoNightDuty,
            PlanningNoWeekends = driver.PlanningNoWeekends,
            PlanningNoSaturdays = driver.PlanningNoSaturdays,
            PlanningNoHolidays = driver.PlanningNoHolidays,
            PlanningNoDaysOff = driver.PlanningNoDaysOff,
            CreatedAtUtc = driver.CreatedAtUtc
        };
    }

    public async Task<DriverDto?> UpdateOperatingCompanyAsync(
        Guid id,
        UpdateDriverOperatingCompanyDto dto,
        CancellationToken cancellationToken = default)
    {
        var driver = await _dbContext.Drivers
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId
                && (!_currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == _currentUser.OperatingCompanyId.Value), cancellationToken);
        if (driver is null) return null;

        if (_currentUser.OperatingCompanyId.HasValue
            && dto.OperatingCompanyId != _currentUser.OperatingCompanyId)
            throw new InvalidOperationException("Konto firmy nie może przenosić kierowców do innej firmy.");

        OperatingCompany? operatingCompany = null;
        if (dto.OperatingCompanyId.HasValue)
        {
            operatingCompany = await _dbContext.OperatingCompanies
                .FirstOrDefaultAsync(
                    x => x.Id == dto.OperatingCompanyId.Value && x.CompanyId == _currentUser.CompanyId,
                    cancellationToken);
            if (operatingCompany is null) throw new ArgumentException("Wybrana firma nie istnieje.");
        }

        driver.OperatingCompanyId = operatingCompany?.Id;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DriverDto
        {
            Id = driver.Id,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            CardNumber = driver.CardNumber,
            CardExpiryDate = driver.CardExpiryDate,
            CardIssuingCountry = driver.CardIssuingCountry,
            OperatingCompanyId = driver.OperatingCompanyId,
            OperatingCompanyName = operatingCompany?.Name,
            IncludeInPlanning = driver.IncludeInPlanning,
            IsActive = driver.IsActive,
            PlanningNoNightDuty = driver.PlanningNoNightDuty,
            PlanningNoWeekends = driver.PlanningNoWeekends,
            PlanningNoSaturdays = driver.PlanningNoSaturdays,
            PlanningNoHolidays = driver.PlanningNoHolidays,
            PlanningNoDaysOff = driver.PlanningNoDaysOff,
            CreatedAtUtc = driver.CreatedAtUtc
        };
    }
    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        if (!_currentUser.IsAuthenticated || companyId == Guid.Empty || _currentUser.IsMobileDriver) return false;
        var driver = await _dbContext.Drivers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.Id == id && x.CompanyId == companyId
                    && (!_currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == _currentUser.OperatingCompanyId.Value),
                cancellationToken);

        if (driver is null)
        {
            return false;
        }

        if (driver.DeletedAtUtc.HasValue) return true;
        driver.WasActiveBeforeDeletion = driver.IsActive;
        driver.DeletedAtUtc = DateTime.UtcNow;
        driver.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<Driver> DeletedDriversInScope() => _dbContext.Drivers.IgnoreQueryFilters()
        .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAtUtc != null
            && (!_currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == _currentUser.OperatingCompanyId));

    public async Task<List<DeletedDriverDto>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty || _currentUser.IsMobileDriver) return [];
        var drivers = await DeletedDriversInScope().Include(x => x.OperatingCompany)
            .OrderByDescending(x => x.DeletedAtUtc).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return drivers.Select(x => new DeletedDriverDto {
            Id = x.Id, FirstName = x.FirstName, LastName = x.LastName,
            OperatingCompanyName = x.OperatingCompany?.Name,
            DeletedAtUtc = x.DeletedAtUtc!.Value, RestoreUntilUtc = x.DeletedAtUtc.Value.AddMonths(1),
            CanRestore = now < x.DeletedAtUtc.Value.AddMonths(1)
        }).ToList();
    }

    public async Task<bool> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty || _currentUser.IsMobileDriver) return false;
        var driver = await DeletedDriversInScope().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (driver is null) return false;
        if (DateTime.UtcNow >= driver.DeletedAtUtc!.Value.AddMonths(1))
            throw new InvalidOperationException("Minął miesięczny termin przywrócenia kierowcy. Historia pozostaje zachowana.");
        driver.IsActive = driver.WasActiveBeforeDeletion ?? true;
        driver.WasActiveBeforeDeletion = null;
        driver.DeletedAtUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
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


