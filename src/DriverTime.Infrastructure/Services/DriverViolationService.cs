using System.Globalization;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverViolationService : IDriverViolationService
{
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromHours(4.5);
    private static readonly TimeSpan DailyDrivingLimit = TimeSpan.FromHours(9);
    private static readonly TimeSpan ExtendedDailyDrivingLimit = TimeSpan.FromHours(10);
    private static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan DailyRestLimit = TimeSpan.FromHours(11);
    private static readonly TimeSpan WeeklyDrivingLimit = TimeSpan.FromHours(56);
    private static readonly TimeSpan TwoWeekDrivingLimit = TimeSpan.FromHours(90);

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverViolationService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DriverViolationDto>> GetViolationsAsync(
        CancellationToken cancellationToken = default)
    {
        var activities = await GetActivitiesQuery()
            .OrderBy(x => x.StartUtc)
            .ToListAsync(cancellationToken);

        return CalculateViolations(activities);
    }

    public async Task<IReadOnlyList<DriverViolationDto>?> GetViolationsForDriverAsync(
        Guid driverId,
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

        var activities = await GetActivitiesQuery()
            .Where(x => x.DddFile.DriverId == driverId)
            .OrderBy(x => x.StartUtc)
            .ToListAsync(cancellationToken);

        return CalculateViolations(activities);
    }

    private IQueryable<DriverActivity> GetActivitiesQuery()
    {
        return _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x => x.DddFile.CompanyId == _currentUser.CompanyId)
            .Include(x => x.DddFile);
    }

    private static IReadOnlyList<DriverViolationDto> CalculateViolations(
        IReadOnlyList<DriverActivity> activities)
    {
        var violations = new List<DriverViolationDto>();

        foreach (var driverActivities in activities.GroupBy(GetDriverKey))
        {
            var orderedActivities = driverActivities
                .OrderBy(x => x.StartUtc)
                .ToList();

            AddContinuousDrivingViolations(orderedActivities, violations);
            AddDailyDrivingViolations(orderedActivities, violations);
            AddDailyRestViolations(orderedActivities, violations);
            AddWeeklyDrivingViolations(orderedActivities, violations);
        }

        return violations
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenBy(x => x.Code)
            .ToList();
    }

    private static void AddContinuousDrivingViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var drivingDuration = TimeSpan.Zero;
        DateTime? periodStartUtc = null;
        DriverActivity? lastDrivingActivity = null;

        foreach (var activity in activities)
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
            "CONTINUOUS_DRIVING_OVER_4H30",
            "Brak pauzy po 4h30 jazdy",
            periodStartUtc ?? activity.StartUtc,
            activity.EndUtc,
            drivingDuration,
            ContinuousDrivingLimit,
            $"Jazda bez wymaganej pauzy 45 minut trwala {FormatDuration(drivingDuration)}.",
            "high"));
    }

    private static void AddDailyDrivingViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        foreach (var day in activities
                     .Where(x => IsActivity(x, "DRIVING"))
                     .GroupBy(x => x.StartUtc.Date))
        {
            var drivingDuration = SumDuration(day);

            if (drivingDuration <= DailyDrivingLimit)
            {
                continue;
            }

            var firstActivity = day.OrderBy(x => x.StartUtc).First();
            var exceedsExtendedLimit = drivingDuration > ExtendedDailyDrivingLimit;
            var limit = exceedsExtendedLimit
                ? ExtendedDailyDrivingLimit
                : DailyDrivingLimit;

            violations.Add(CreateViolation(
                firstActivity,
                exceedsExtendedLimit
                    ? "DAILY_DRIVING_OVER_10H"
                    : "DAILY_DRIVING_OVER_9H",
                exceedsExtendedLimit
                    ? "Przekroczony dzienny czas jazdy 10h"
                    : "Przekroczony dzienny czas jazdy 9h",
                day.Key,
                day.Max(x => x.EndUtc),
                drivingDuration,
                limit,
                $"Dzienny czas jazdy wyniosl {FormatDuration(drivingDuration)}.",
                exceedsExtendedLimit ? "high" : "medium"));
        }
    }

    private static void AddDailyRestViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        foreach (var day in activities.GroupBy(x => x.StartUtc.Date))
        {
            var drivingActivities = day
                .Where(x => IsActivity(x, "DRIVING"))
                .ToList();

            if (drivingActivities.Count == 0)
            {
                continue;
            }

            var restDuration = SumDuration(day.Where(x => IsActivity(x, "REST")));

            if (restDuration >= DailyRestLimit)
            {
                continue;
            }

            violations.Add(CreateViolation(
                drivingActivities.OrderBy(x => x.StartUtc).First(),
                "DAILY_REST_BELOW_11H",
                "Odpoczynek dzienny ponizej 11h",
                day.Key,
                day.Key.AddDays(1),
                restDuration,
                DailyRestLimit,
                $"Laczny odpoczynek zapisany w tym dniu wyniosl {FormatDuration(restDuration)}.",
                "high"));
        }
    }

    private static void AddWeeklyDrivingViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var weeks = activities
            .Where(x => IsActivity(x, "DRIVING"))
            .GroupBy(x => GetWeekStart(x.StartUtc))
            .Select(group => new WeekDrivingSummary(
                group.Key,
                group.OrderBy(x => x.StartUtc).First(),
                SumDuration(group)))
            .OrderBy(x => x.WeekStartUtc)
            .ToList();

        foreach (var week in weeks.Where(x => x.DrivingDuration > WeeklyDrivingLimit))
        {
            violations.Add(CreateViolation(
                week.FirstActivity,
                "WEEKLY_DRIVING_OVER_56H",
                "Przekroczony tygodniowy czas jazdy 56h",
                week.WeekStartUtc,
                week.WeekStartUtc.AddDays(7),
                week.DrivingDuration,
                WeeklyDrivingLimit,
                $"Tygodniowy czas jazdy wyniosl {FormatDuration(week.DrivingDuration)}.",
                "high"));
        }

        for (var index = 1; index < weeks.Count; index++)
        {
            var previousWeek = weeks[index - 1];
            var currentWeek = weeks[index];

            if (currentWeek.WeekStartUtc != previousWeek.WeekStartUtc.AddDays(7))
            {
                continue;
            }

            var twoWeekDuration =
                previousWeek.DrivingDuration + currentWeek.DrivingDuration;

            if (twoWeekDuration <= TwoWeekDrivingLimit)
            {
                continue;
            }

            violations.Add(CreateViolation(
                previousWeek.FirstActivity,
                "TWO_WEEK_DRIVING_OVER_90H",
                "Przekroczony czas jazdy 90h w dwoch tygodniach",
                previousWeek.WeekStartUtc,
                currentWeek.WeekStartUtc.AddDays(7),
                twoWeekDuration,
                TwoWeekDrivingLimit,
                $"Czas jazdy w dwoch kolejnych tygodniach wyniosl {FormatDuration(twoWeekDuration)}.",
                "high"));
        }
    }

    private static DriverViolationDto CreateViolation(
        DriverActivity activity,
        string code,
        string violationType,
        DateTime occurredAtUtc,
        DateTime periodEndUtc,
        TimeSpan actualDuration,
        TimeSpan limitDuration,
        string description,
        string severity)
    {
        return new DriverViolationDto
        {
            Code = code,
            DriverFirstName = activity.DddFile.DriverFirstName,
            DriverLastName = activity.DddFile.DriverLastName,
            DriverCardNumber = activity.DddFile.DriverCardNumber,
            ViolationType = violationType,
            OccurredAtUtc = occurredAtUtc,
            PeriodEndUtc = periodEndUtc,
            Description = description,
            Severity = severity,
            ActualDurationMinutes = (long)actualDuration.TotalMinutes,
            LimitDurationMinutes = (long)limitDuration.TotalMinutes
        };
    }

    private static string GetDriverKey(DriverActivity activity)
    {
        return activity.DddFile.DriverId?.ToString()
            ?? (string.IsNullOrWhiteSpace(activity.DddFile.DriverCardNumber)
                ? activity.DddFileId.ToString()
                : activity.DddFile.DriverCardNumber);
    }

    private static DateTime GetWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);
        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
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

    private static TimeSpan SumDuration(IEnumerable<DriverActivity> activities)
    {
        return activities.Aggregate(
            TimeSpan.Zero,
            (total, activity) => total + GetDuration(activity));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
    }

    private sealed record WeekDrivingSummary(
        DateTime WeekStartUtc,
        DriverActivity FirstActivity,
        TimeSpan DrivingDuration);
}
