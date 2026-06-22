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
        var displayedVehicleUseIds = new HashSet<Guid>();

        return activities
            .Select(activity =>
            {
                var durationSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                    activity.StartUtc,
                    activity.EndUtc);
                var vehicleUse = FindBestVehicleUse(activity, vehicleUses);
                var vehicleRegistration = vehicleUse?.RegistrationNumber.Trim() ?? string.Empty;
                var shouldShowOdometer = ShouldShowOdometer(vehicleUse, displayedVehicleUseIds);

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
                        : (int)durationSeconds,
                    StartOdometerKm = shouldShowOdometer ? vehicleUse?.StartOdometerKm : null,
                    EndOdometerKm = shouldShowOdometer ? vehicleUse?.EndOdometerKm : null,
                    DistanceKm = shouldShowOdometer ? vehicleUse?.DistanceKm : null
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
                Id = x.Id,
                DddFileId = x.DddFileId,
                RegistrationNumber = x.RegistrationNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                StartOdometerKm = x.StartOdometerKm,
                EndOdometerKm = x.EndOdometerKm,
                DistanceKm = x.DistanceKm
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
                x.StartUtc,
                x.EndUtc,
                ActivityType = x.ActivityType.ToUpperInvariant()
            })
            .Select(x => x.OrderBy(activity => activity.Id).First())
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
    }

    private static VehicleUseReportSource? FindBestVehicleUse(
        DriverActivityReportSource activity,
        IReadOnlyCollection<VehicleUseReportSource> vehicleUses)
    {
        return vehicleUses
            .Where(x =>
                x.DddFileId == activity.DddFileId
                && x.StartUtc < activity.EndUtc
                && x.EndUtc > activity.StartUtc)
            .Select(x => new
            {
                VehicleUse = x,
                IsContaining = x.StartUtc <= activity.StartUtc && x.EndUtc >= activity.EndUtc,
                OverlapSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                    x.StartUtc > activity.StartUtc ? x.StartUtc : activity.StartUtc,
                    x.EndUtc < activity.EndUtc ? x.EndUtc : activity.EndUtc)
            })
            .OrderByDescending(x => x.IsContaining)
            .ThenByDescending(x => x.OverlapSeconds)
            .Select(x => x.VehicleUse)
            .FirstOrDefault();
    }

    private static bool ShouldShowOdometer(
        VehicleUseReportSource? vehicleUse,
        ISet<Guid> displayedVehicleUseIds)
    {
        return vehicleUse is not null
            && displayedVehicleUseIds.Add(vehicleUse.Id);
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
        public Guid Id { get; set; }

        public Guid DddFileId { get; set; }

        public string RegistrationNumber { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public int? StartOdometerKm { get; set; }

        public int? EndOdometerKm { get; set; }

        public int? DistanceKm { get; set; }
    }
}
