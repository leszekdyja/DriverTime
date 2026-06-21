using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DriverTime.Infrastructure.Services;

public class DriverActivityCalendarService : IDriverActivityCalendarService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverActivityCalendarService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DriverActivityCalendarDto?> GetAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var driver = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId)
            .Select(x => new
            {
                x.FirstName,
                x.LastName,
                x.CardNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (driver is null)
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
        var violations = await _dbContext.Violations
            .AsNoTracking()
            .Where(x =>
                x.DriverId == driverId
                && x.Driver != null
                && x.Driver.CompanyId == _currentUser.CompanyId
                && x.ViolationEnd >= fromUtc
                && x.ViolationStart < toUtcExclusive)
            .OrderBy(x => x.ViolationStart)
            .Select(x => new
            {
                x.RegulationReference,
                x.ViolationType,
                x.ViolationStart,
                x.ViolationEnd,
                x.Severity,
                x.DurationMinutes,
                x.MetadataJson
            })
            .ToListAsync(cancellationToken);
        var violationDtos = violations
            .Select(x => MapViolation(
                x.RegulationReference,
                x.ViolationType,
                x.ViolationStart,
                x.ViolationEnd,
                x.Severity,
                x.DurationMinutes,
                x.MetadataJson,
                driver.FirstName,
                driver.LastName,
                driver.CardNumber))
            .ToList();
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

            var hasActivityMinutes =
                day.DrivingSeconds
                + day.WorkSeconds
                + day.RestSeconds
                + day.AvailabilitySeconds
                + day.OtherSeconds > 0;

            day.Violations = hasActivityMinutes
                ? violationDtos
                    .Where(x => GetViolationPresentationDate(x) == date)
                    .OrderBy(x => x.OccurredAtUtc)
                    .ToList()
                : [];
            result.Days.Add(day);
        }

        return result;
    }

    private static DateTime ToUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DriverViolationDto MapViolation(
        string code,
        string violationType,
        DateTime occurredAtUtc,
        DateTime periodEndUtc,
        string severity,
        int durationMinutes,
        string metadataJson,
        string firstName,
        string lastName,
        string cardNumber)
    {
        return new DriverViolationDto
        {
            Code = code,
            DriverFirstName = firstName,
            DriverLastName = lastName,
            DriverCardNumber = cardNumber,
            ViolationType = violationType,
            OccurredAtUtc = occurredAtUtc,
            PeriodEndUtc = periodEndUtc,
            Description = violationType,
            Severity = severity,
            ActualDurationMinutes = durationMinutes,
            LimitDurationMinutes = 0,
            Metadata = ParseMetadata(metadataJson)
        };
    }

    private static Dictionary<string, object> ParseMetadata(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson)
                ?? new Dictionary<string, object>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object>();
        }
    }

    private static DateOnly GetViolationPresentationDate(
        DriverViolationDto violation)
    {
        var code = violation.Code.ToUpperInvariant();
        var type = violation.ViolationType.ToUpperInvariant();

        if (code.Contains("COMPENSATION", StringComparison.Ordinal) ||
            type.Contains("COMPENSATION", StringComparison.Ordinal))
        {
            return DateOnly.FromDateTime(violation.PeriodEndUtc.Date);
        }

        if (code.Contains("WEEKLY", StringComparison.Ordinal) ||
            type.Contains("WEEKLY", StringComparison.Ordinal))
        {
            var endUtc = violation.PeriodEndUtc > violation.OccurredAtUtc
                ? violation.PeriodEndUtc.AddTicks(-1)
                : violation.OccurredAtUtc;

            return DateOnly.FromDateTime(endUtc.Date);
        }

        return DateOnly.FromDateTime(violation.OccurredAtUtc.Date);
    }

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
