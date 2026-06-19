using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverActivityCalendarService : IDriverActivityCalendarService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IComplianceEngineService _complianceEngineService;

    public DriverActivityCalendarService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IComplianceEngineService complianceEngineService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _complianceEngineService = complianceEngineService;
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
        var preview = await _complianceEngineService.PreviewForDriverAsync(
            _currentUser.CompanyId,
            driverId,
            includeTimeline: false,
            cancellationToken);
        var violations = preview?.Violations
            .Select(x => MapViolation(x, driver.FirstName, driver.LastName, driver.CardNumber))
            .ToList()
            ?? [];
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
                .Where(x => GetViolationPresentationDate(x) == date)
                .OrderBy(x => x.OccurredAtUtc)
                .ToList();
            result.Days.Add(day);
        }

        return result;
    }

    private static DateTime ToUtc(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DriverViolationDto MapViolation(
        ComplianceViolationPreviewDto violation,
        string firstName,
        string lastName,
        string cardNumber)
    {
        return new DriverViolationDto
        {
            Code = violation.Code,
            DriverFirstName = firstName,
            DriverLastName = lastName,
            DriverCardNumber = cardNumber,
            ViolationType = violation.RuleName,
            OccurredAtUtc = violation.PeriodStartUtc,
            PeriodEndUtc = violation.PeriodEndUtc,
            Description = violation.Description,
            Severity = violation.Severity,
            ActualDurationMinutes = violation.ActualMinutes,
            LimitDurationMinutes = violation.LimitMinutes
        };
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
