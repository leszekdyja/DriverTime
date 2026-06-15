using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverViolationService : IDriverViolationService
{
    private static readonly TimeSpan ContinuousDrivingLimit =
        TimeSpan.FromHours(4.5);

    private static readonly TimeSpan DailyDrivingLimit =
        TimeSpan.FromHours(9);

    private static readonly TimeSpan RequiredBreak =
        TimeSpan.FromMinutes(45);

    private readonly DriverTimeDbContext _dbContext;

    public DriverViolationService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DriverViolationDto>> GetViolationsAsync(
        CancellationToken cancellationToken = default)
    {
        var activities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Include(x => x.DddFile)
            .OrderBy(x => x.StartUtc)
            .ToListAsync(cancellationToken);

        var violations = new List<DriverViolationDto>();

        foreach (var driverActivities in activities.GroupBy(GetDriverKey))
        {
            AddContinuousDrivingViolations(driverActivities, violations);
            AddDailyDrivingViolations(driverActivities, violations);
        }

        return violations
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToList();
    }

    private static void AddContinuousDrivingViolations(
        IEnumerable<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var drivingDuration = TimeSpan.Zero;
        DateTime? periodStartUtc = null;
        DriverActivity? lastDrivingActivity = null;

        foreach (var activity in activities.OrderBy(x => x.StartUtc))
        {
            var duration = GetDuration(activity);

            if (IsActivity(activity, "DRIVING"))
            {
                periodStartUtc ??= activity.StartUtc;
                drivingDuration += duration;
                lastDrivingActivity = activity;
                continue;
            }

            if (IsActivity(activity, "REST") && duration >= RequiredBreak)
            {
                AddContinuousDrivingViolation(
                    lastDrivingActivity,
                    periodStartUtc,
                    drivingDuration,
                    violations);

                drivingDuration = TimeSpan.Zero;
                periodStartUtc = null;
                lastDrivingActivity = null;
            }
        }

        AddContinuousDrivingViolation(
            lastDrivingActivity,
            periodStartUtc,
            drivingDuration,
            violations);
    }

    private static void AddContinuousDrivingViolation(
        DriverActivity? activity,
        DateTime? periodStartUtc,
        TimeSpan drivingDuration,
        ICollection<DriverViolationDto> violations)
    {
        if (activity is null || drivingDuration <= ContinuousDrivingLimit)
        {
            return;
        }

        violations.Add(CreateViolation(
            activity,
            "Przekroczony czas jazdy ciaglej",
            periodStartUtc ?? activity.StartUtc,
            $"Jazda ciagla trwala {FormatDuration(drivingDuration)} bez wymaganego odpoczynku 45 minut.",
            "high"));
    }

    private static void AddDailyDrivingViolations(
        IEnumerable<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var drivingByDay = activities
            .Where(x => IsActivity(x, "DRIVING"))
            .GroupBy(x => x.StartUtc.Date);

        foreach (var day in drivingByDay)
        {
            var drivingDuration = day.Aggregate(
                TimeSpan.Zero,
                (total, activity) => total + GetDuration(activity));

            if (drivingDuration <= DailyDrivingLimit)
            {
                continue;
            }

            var firstActivity = day.OrderBy(x => x.StartUtc).First();

            violations.Add(CreateViolation(
                firstActivity,
                "Przekroczony dzienny czas jazdy",
                day.Key,
                $"Dzienny czas jazdy wyniosl {FormatDuration(drivingDuration)} i przekroczyl limit 9 godzin.",
                "medium"));
        }
    }

    private static DriverViolationDto CreateViolation(
        DriverActivity activity,
        string violationType,
        DateTime occurredAtUtc,
        string description,
        string severity)
    {
        return new DriverViolationDto
        {
            DriverFirstName = activity.DddFile.DriverFirstName,
            DriverLastName = activity.DddFile.DriverLastName,
            DriverCardNumber = activity.DddFile.DriverCardNumber,
            ViolationType = violationType,
            OccurredAtUtc = occurredAtUtc,
            Description = description,
            Severity = severity
        };
    }

    private static string GetDriverKey(DriverActivity activity)
    {
        return string.IsNullOrWhiteSpace(activity.DddFile.DriverCardNumber)
            ? activity.DddFileId.ToString()
            : activity.DddFile.DriverCardNumber;
    }

    private static bool IsActivity(
        DriverActivity activity,
        string activityType)
    {
        return activity.ActivityType.Equals(
            activityType,
            StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetDuration(DriverActivity activity)
    {
        var duration = activity.EndUtc - activity.StartUtc;

        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
    }
}
