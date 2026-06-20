using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverActivityService : IDriverActivityService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverActivityService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<DriverActivityDto>> GetActivitiesAsync(
        DateTime? from,
        DateTime? to,
        string? driverCardNumber)
    {
        var query = _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x => x.DddFile.CompanyId == _currentUser.CompanyId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(driverCardNumber))
        {
            query = query.Where(
                x => x.DddFile.DriverCardNumber == driverCardNumber);
        }

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(
                from.Value,
                DateTimeKind.Utc);

            query = query.Where(x => x.EndUtc > fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(
                to.Value,
                DateTimeKind.Utc);

            query = query.Where(x => x.StartUtc < toUtc);
        }

        var activityRows = await query
            .OrderBy(x => x.StartUtc)
            .Select(x => new DriverActivityReportSource
            {
                Id = x.Id,
                DddFileId = x.DddFileId,
                DriverId = x.DddFile.DriverId,
                DriverFirstName = x.DddFile.DriverFirstName,
                DriverLastName = x.DddFile.DriverLastName,
                DriverCardNumber = x.DddFile.DriverCardNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                ActivityType = x.ActivityType
            })
            .ToListAsync();

        var activities = DeduplicateActivities(activityRows);
        var vehicleUses = await GetVehicleUsesAsync(activities);

        return activities
            .Select(activity =>
            {
                var durationSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                    activity.StartUtc,
                    activity.EndUtc);
                var vehicleRegistration = FindVehicleRegistration(activity, vehicleUses);

                return new DriverActivityDto
                {
                    Id = activity.Id,
                    DddFileId = activity.DddFileId,
                    DriverFirstName = activity.DriverFirstName,
                    DriverLastName = activity.DriverLastName,
                    DriverCardNumber = activity.DriverCardNumber,
                    VehicleRegistration = vehicleRegistration,
                    VehicleRegistrationNumber = vehicleRegistration,
                    Vehicle = vehicleRegistration,
                    StartUtc = activity.StartUtc,
                    EndUtc = activity.EndUtc,
                    ActivityType = activity.ActivityType,
                    DurationSeconds = durationSeconds > int.MaxValue
                        ? int.MaxValue
                        : (int)durationSeconds
                };
            })
            .ToList();
    }

    private async Task<List<VehicleUseReportSource>> GetVehicleUsesAsync(
        IReadOnlyCollection<DriverActivityReportSource> activities)
    {
        if (activities.Count == 0)
        {
            return new List<VehicleUseReportSource>();
        }

        var dddFileIds = activities.Select(x => x.DddFileId).Distinct().ToList();
        var fromUtc = activities.Min(x => x.StartUtc);
        var toUtc = activities.Max(x => x.EndUtc);

        return await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                dddFileIds.Contains(x.DddFileId)
                && x.StartUtc < toUtc
                && x.EndUtc > fromUtc)
            .Select(x => new VehicleUseReportSource
            {
                DddFileId = x.DddFileId,
                RegistrationNumber = x.RegistrationNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc
            })
            .ToListAsync();
    }

    private static List<DriverActivityReportSource> DeduplicateActivities(
        IEnumerable<DriverActivityReportSource> activities)
    {
        return activities
            .GroupBy(x => new
            {
                DriverKey = x.DriverId?.ToString("D") ?? x.DriverCardNumber,
                x.DddFileId,
                x.StartUtc,
                x.EndUtc,
                ActivityType = x.ActivityType.ToUpperInvariant()
            })
            .Select(x => x.OrderBy(activity => activity.Id).First())
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
    }

    private static string FindVehicleRegistration(
        DriverActivityReportSource activity,
        IReadOnlyCollection<VehicleUseReportSource> vehicleUses)
    {
        return vehicleUses
            .Where(x =>
                x.DddFileId == activity.DddFileId
                && x.StartUtc < activity.EndUtc
                && x.EndUtc > activity.StartUtc
                && !string.IsNullOrWhiteSpace(x.RegistrationNumber))
            .Select(x => new
            {
                x.RegistrationNumber,
                IsContaining = x.StartUtc <= activity.StartUtc && x.EndUtc >= activity.EndUtc,
                OverlapSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                    x.StartUtc > activity.StartUtc ? x.StartUtc : activity.StartUtc,
                    x.EndUtc < activity.EndUtc ? x.EndUtc : activity.EndUtc)
            })
            .OrderByDescending(x => x.IsContaining)
            .ThenByDescending(x => x.OverlapSeconds)
            .Select(x => x.RegistrationNumber.Trim())
            .FirstOrDefault() ?? string.Empty;
    }

    private sealed class DriverActivityReportSource
    {
        public Guid Id { get; set; }

        public Guid DddFileId { get; set; }

        public Guid? DriverId { get; set; }

        public string DriverFirstName { get; set; } = string.Empty;

        public string DriverLastName { get; set; } = string.Empty;

        public string DriverCardNumber { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public string ActivityType { get; set; } = string.Empty;
    }

    private sealed class VehicleUseReportSource
    {
        public Guid DddFileId { get; set; }

        public string RegistrationNumber { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }
    }
}
