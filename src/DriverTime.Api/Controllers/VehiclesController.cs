using DriverTime.Application.Interfaces;
using DriverTime.Application.Vehicles.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public VehiclesController(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        var vehicles = await BuildCompanyVehiclesQuery()
            .OrderBy(x => x.RegistrationNumber)
            .Select(x => MapVehicle(x))
            .ToListAsync(cancellationToken);

        return Ok(vehicles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleDetailsDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        var vehicle = await BuildCompanyVehiclesQuery()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (vehicle is null)
        {
            return NotFound();
        }

        var normalizedRegistration = NormalizeVehicleRegistration(vehicle.RegistrationNumber);
        var vehicleUses = BuildVehicleUsesQuery(normalizedRegistration);

        var details = new VehicleDetailsDto
        {
            Id = vehicle.Id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Vin = vehicle.Vin,
            Active = vehicle.Active,
            LastActivityAtUtc = await vehicleUses
                .Select(x => (DateTime?)x.EndUtc)
                .MaxAsync(cancellationToken),
            DddImportsCount = await vehicleUses
                .Select(x => x.DddFileId)
                .Distinct()
                .CountAsync(cancellationToken),
            VehicleUses = await vehicleUses
                .OrderByDescending(x => x.StartUtc)
                .Select(x => new VehicleUseHistoryDto
                {
                    Id = x.Id,
                    DddFileId = x.DddFileId,
                    FileName = x.DddFile.FileName,
                    UploadedAtUtc = x.DddFile.UploadedAtUtc,
                    DriverId = x.DddFile.DriverId,
                    DriverName = x.DddFile.Driver == null
                        ? string.Empty
                        : FormatDriverName(x.DddFile.Driver.FirstName, x.DddFile.Driver.LastName),
                    RegistrationNumber = x.RegistrationNumber,
                    StartUtc = x.StartUtc,
                    EndUtc = x.EndUtc
                })
                .ToListAsync(cancellationToken),
            Drivers = await vehicleUses
                .Where(x => x.DddFile.DriverId != null && x.DddFile.Driver != null)
                .GroupBy(x => new
                {
                    DriverId = x.DddFile.DriverId!.Value,
                    x.DddFile.Driver!.FirstName,
                    x.DddFile.Driver.LastName,
                    x.DddFile.Driver.CardNumber
                })
                .Select(group => new VehicleDriverDto
                {
                    DriverId = group.Key.DriverId,
                    FirstName = group.Key.FirstName,
                    LastName = group.Key.LastName,
                    CardNumber = group.Key.CardNumber,
                    FirstUsedAtUtc = group.Min(x => x.StartUtc),
                    LastUsedAtUtc = group.Max(x => x.EndUtc),
                    UsageCount = group.Count()
                })
                .OrderByDescending(x => x.LastUsedAtUtc)
                .ToListAsync(cancellationToken)
        };

        details.Activities = await BuildVehicleActivitiesQuery(normalizedRegistration)
            .OrderByDescending(x => x.StartUtc)
            .Select(x => new VehicleActivityDto
            {
                Id = x.ActivityId,
                DddFileId = x.DddFileId,
                DriverId = x.DriverId,
                DriverName = x.DriverName,
                ActivityType = x.ActivityType,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(details);
    }

    [HttpGet("{id:guid}/analytics")]
    public async Task<ActionResult<VehicleAnalyticsDto>> GetAnalytics(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        var vehicle = await BuildCompanyVehiclesQuery()
            .Where(x => x.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (vehicle is null)
        {
            return NotFound();
        }

        var normalizedRegistration = NormalizeVehicleRegistration(vehicle.RegistrationNumber);
        var nowUtc = DateTime.UtcNow;
        var last7DaysStart = nowUtc.AddDays(-7);
        var last30DaysStart = nowUtc.Date.AddDays(-29);

        var vehicleUses = await BuildVehicleUsesQuery(normalizedRegistration)
            .Select(x => new VehicleUseAnalyticsSource
            {
                DddFileId = x.DddFileId,
                DriverId = x.DddFile.DriverId,
                DriverFirstName = x.DddFile.Driver == null ? string.Empty : x.DddFile.Driver.FirstName,
                DriverLastName = x.DddFile.Driver == null ? string.Empty : x.DddFile.Driver.LastName,
                DriverCardNumber = x.DddFile.Driver == null ? string.Empty : x.DddFile.Driver.CardNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc
            })
            .ToListAsync(cancellationToken);

        var totalUsageMinutes = vehicleUses.Sum(x => GetUsageMinutes(x.StartUtc, x.EndUtc));
        var activeDays = vehicleUses
            .Select(x => DateOnly.FromDateTime(x.StartUtc.Date))
            .Distinct()
            .Count();

        var dailyUsage = Enumerable.Range(0, 30)
            .Select(offset => DateOnly.FromDateTime(last30DaysStart.AddDays(offset)))
            .Select(day =>
            {
                var dayUses = vehicleUses
                    .Where(x => DateOnly.FromDateTime(x.StartUtc.Date) == day)
                    .ToList();

                return new VehicleDailyUsageDto
                {
                    Date = day,
                    UsesCount = dayUses.Count,
                    UsageMinutes = dayUses.Sum(x => GetUsageMinutes(x.StartUtc, x.EndUtc))
                };
            })
            .ToList();

        var analytics = new VehicleAnalyticsDto
        {
            VehicleId = vehicle.Id,
            RegistrationNumber = vehicle.RegistrationNumber,
            TotalUses = vehicleUses.Count,
            TotalDrivers = vehicleUses
                .Where(x => x.DriverId.HasValue)
                .Select(x => x.DriverId!.Value)
                .Distinct()
                .Count(),
            TotalDddImports = vehicleUses
                .Select(x => x.DddFileId)
                .Distinct()
                .Count(),
            FirstUseUtc = vehicleUses.Count == 0 ? null : vehicleUses.Min(x => x.StartUtc),
            LastUseUtc = vehicleUses.Count == 0 ? null : vehicleUses.Max(x => x.EndUtc),
            TotalUsageMinutes = totalUsageMinutes,
            TotalUsageHours = Math.Round(totalUsageMinutes / 60m, 2),
            ActiveDays = activeDays,
            AverageUsageMinutesPerActiveDay = activeDays == 0
                ? 0
                : Math.Round(totalUsageMinutes / (decimal)activeDays, 2),
            UsesLast7Days = vehicleUses.Count(x => x.StartUtc >= last7DaysStart),
            UsesLast30Days = vehicleUses.Count(x => x.StartUtc >= last30DaysStart),
            DailyUsageLast30Days = dailyUsage,
            DriverUsage = vehicleUses
                .Where(x => x.DriverId.HasValue)
                .GroupBy(x => new
                {
                    DriverId = x.DriverId!.Value,
                    x.DriverFirstName,
                    x.DriverLastName,
                    x.DriverCardNumber
                })
                .Select(group => new VehicleDriverUsageDto
                {
                    DriverId = group.Key.DriverId,
                    DriverName = FormatDriverName(group.Key.DriverFirstName, group.Key.DriverLastName),
                    CardNumber = group.Key.DriverCardNumber,
                    UsesCount = group.Count(),
                    UsageMinutes = group.Sum(x => GetUsageMinutes(x.StartUtc, x.EndUtc)),
                    FirstUseUtc = group.Min(x => x.StartUtc),
                    LastUseUtc = group.Max(x => x.EndUtc)
                })
                .OrderByDescending(x => x.UsesCount)
                .ThenByDescending(x => x.UsageMinutes)
                .ToList()
        };

        return Ok(analytics);
    }

    private IQueryable<Vehicle> BuildCompanyVehiclesQuery()
    {
        return _dbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == _currentUser.CompanyId
                && x.Active
                && x.RegistrationNumber.Replace(" ", "").Length >= 5);
    }

    private IQueryable<VehicleUse> BuildVehicleUsesQuery(string normalizedRegistration)
    {
        var nowUtc = DateTime.UtcNow;
        var latestAllowedUtc = nowUtc.AddDays(1);

        return _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.StartUtc >= VehicleUseDateValidator.MinimumStartUtc
                && x.EndUtc > x.StartUtc
                && x.StartUtc <= latestAllowedUtc
                && x.EndUtc <= latestAllowedUtc
                && x.RegistrationNumber != null
                && x.RegistrationNumber.Replace(" ", "").Length >= 5
                && EF.Functions.Like(
                    normalizedRegistration,
                    "%" + x.RegistrationNumber.Replace(" ", "").ToUpper()));
    }

    private IQueryable<VehicleActivitySource> BuildVehicleActivitiesQuery(string normalizedRegistration)
    {
        var nowUtc = DateTime.UtcNow;
        var latestAllowedUtc = nowUtc.AddDays(1);

        return _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.StartUtc >= VehicleUseDateValidator.MinimumStartUtc
                && x.EndUtc > x.StartUtc
                && x.StartUtc <= latestAllowedUtc
                && x.EndUtc <= latestAllowedUtc
                && x.RegistrationNumber != null
                && x.RegistrationNumber.Replace(" ", "").Length >= 5
                && EF.Functions.Like(
                    normalizedRegistration,
                    "%" + x.RegistrationNumber.Replace(" ", "").ToUpper()))
            .Join(
                _dbContext.DriverActivities.AsNoTracking(),
                vehicleUse => vehicleUse.DddFileId,
                activity => activity.DddFileId,
                (vehicleUse, activity) => new { vehicleUse, activity })
            .Where(x =>
                x.activity.StartUtc < x.vehicleUse.EndUtc
                && x.activity.EndUtc > x.vehicleUse.StartUtc)
            .Select(x => new VehicleActivitySource
            {
                ActivityId = x.activity.Id,
                DddFileId = x.activity.DddFileId,
                DriverId = x.vehicleUse.DddFile.DriverId,
                DriverName = x.vehicleUse.DddFile.Driver == null
                    ? null
                    : FormatDriverName(x.vehicleUse.DddFile.Driver.FirstName, x.vehicleUse.DddFile.Driver.LastName),
                ActivityType = x.activity.ActivityType,
                StartUtc = x.activity.StartUtc < x.vehicleUse.StartUtc
                    ? x.vehicleUse.StartUtc
                    : x.activity.StartUtc,
                EndUtc = x.activity.EndUtc > x.vehicleUse.EndUtc
                    ? x.vehicleUse.EndUtc
                    : x.activity.EndUtc
            });
    }

    private static VehicleDto MapVehicle(Vehicle vehicle)
    {
        return new VehicleDto
        {
            Id = vehicle.Id,
            RegistrationNumber = vehicle.RegistrationNumber,
            Vin = vehicle.Vin,
            Active = vehicle.Active
        };
    }

    private static string NormalizeVehicleRegistration(string? registrationNumber)
    {
        return string.IsNullOrWhiteSpace(registrationNumber)
            ? string.Empty
            : new string(registrationNumber
                .Where(x => !char.IsWhiteSpace(x))
                .ToArray())
                .ToUpperInvariant();
    }

    private static int GetUsageMinutes(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
        {
            return 0;
        }

        return (int)Math.Round((endUtc - startUtc).TotalMinutes, MidpointRounding.AwayFromZero);
    }

    private static string FormatDriverName(string firstName, string lastName)
    {
        return string.Join(
            " ",
            new[] { lastName, firstName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
    }

    private sealed class VehicleUseAnalyticsSource
    {
        public Guid DddFileId { get; set; }

        public Guid? DriverId { get; set; }

        public string DriverFirstName { get; set; } = string.Empty;

        public string DriverLastName { get; set; } = string.Empty;

        public string DriverCardNumber { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }
    }

    private sealed class VehicleActivitySource
    {
        public Guid ActivityId { get; set; }

        public Guid DddFileId { get; set; }

        public Guid? DriverId { get; set; }

        public string? DriverName { get; set; }

        public string ActivityType { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }
    }
}
