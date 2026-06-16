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
            activity.DurationSeconds = GetDurationSeconds(
                activity.StartUtc,
                activity.EndUtc);

            AddDuration(driver, activity.ActivityType, activity.DurationSeconds);
        }

        driver.ImportsCount = imports.Count;
        driver.LastImportAtUtc = imports.FirstOrDefault()?.UploadedAtUtc;
        driver.RecentImports = imports.Take(10).ToList();
        driver.RecentActivities = activities.Take(20).ToList();
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

    private static long GetDurationSeconds(DateTime start, DateTime end) =>
        end > start ? (long)(end - start).TotalSeconds : 0;

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
}
