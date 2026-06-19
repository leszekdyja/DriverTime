using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverActivityCalendarService : IDriverActivityCalendarService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDriverViolationService _driverViolationService;

    public DriverActivityCalendarService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IDriverViolationService driverViolationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _driverViolationService = driverViolationService;
    }

    public async Task<DriverActivityCalendarDto?> GetAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId,
                cancellationToken);

        if (!driverExists)
        {
            return null;
        }

        var fromUtc = ToUtc(from);
        var toUtcExclusive = ToUtc(to.AddDays(1));
        var activities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.DddFile.DriverId == driverId
                && x.StartUtc < toUtcExclusive
                && x.EndUtc > fromUtc)
            .OrderBy(x => x.StartUtc)
            .Select(x => new
            {
                x.Id,
                x.StartUtc,
                x.EndUtc,
                x.ActivityType
            })
            .ToListAsync(cancellationToken);
        var violations = await _driverViolationService
            .GetViolationsForDriverAsync(driverId, cancellationToken)
            ?? Array.Empty<DriverViolationDto>();
        var result = new DriverActivityCalendarDto
        {
            DriverId = driverId,
            From = from,
            To = to
        };

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var dayStart = ToUtc(date);
            var dayEnd = ToUtc(date.AddDays(1));
            var day = new DriverActivityCalendarDayDto { Date = date };

            var dayActivities = ActivityIntervalAggregationHelper.ClipAndMergeByType(
                activities
                    .Where(x => x.StartUtc < dayEnd && x.EndUtc > dayStart)
                    .Select(x => new ActivityInterval(
                        x.Id,
                        x.ActivityType,
                        x.StartUtc,
                        x.EndUtc)),
                dayStart,
                dayEnd);

            foreach (var activity in dayActivities)
            {
                var seconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                    activity.StartUtc,
                    activity.EndUtc);

                if (seconds <= 0)
                {
                    continue;
                }

                day.Activities.Add(new DriverActivityCalendarItemDto
                {
                    Id = activity.Id,
                    StartUtc = activity.StartUtc,
                    EndUtc = activity.EndUtc,
                    ActivityType = activity.ActivityType,
                    DurationSeconds = seconds
                });
                AddDuration(day, activity.ActivityType, seconds);
            }

            day.Violations = violations
                .Where(x => x.OccurredAtUtc < dayEnd && GetViolationEnd(x) > dayStart)
                .OrderBy(x => x.OccurredAtUtc)
                .ToList();
            result.Days.Add(day);
        }

        return result;
    }

    private static DateTime ToUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime GetViolationEnd(
        DriverViolationDto violation) =>
        violation.PeriodEndUtc > violation.OccurredAtUtc
            ? violation.PeriodEndUtc
            : violation.OccurredAtUtc.AddTicks(1);

    private static void AddDuration(
        DriverActivityCalendarDayDto day,
        string activityType,
        long seconds)
    {
        switch (activityType.ToUpperInvariant())
        {
            case "DRIVING":
                day.DrivingSeconds += seconds;
                break;
            case "WORK":
                day.WorkSeconds += seconds;
                break;
            case "REST":
                day.RestSeconds += seconds;
                break;
            case "AVAILABILITY":
                day.AvailabilitySeconds += seconds;
                break;
            default:
                day.OtherSeconds += seconds;
                break;
        }
    }
}
