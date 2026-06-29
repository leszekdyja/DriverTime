using System.Globalization;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningScheduleValidationService : IPlanningScheduleValidationService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PlanningScheduleValidationService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PlanningScheduleValidationDto?> ValidateScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var schedule = await _dbContext.PlanningSchedules
            .AsNoTracking()
            .Include(x => x.Assignments)
                .ThenInclude(x => x.Driver)
            .Include(x => x.Assignments)
                .ThenInclude(x => x.PlanningDuty)
            .Where(x => x.Id == scheduleId && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        return schedule is null ? null : Validate(schedule);
    }

    internal static PlanningScheduleValidationDto Validate(PlanningSchedule schedule)
    {
        var result = new PlanningScheduleValidationDto
        {
            ScheduleId = schedule.Id
        };

        AddDutyConsistencyWarnings(schedule, result.Warnings);
        AddShortRestWarnings(schedule, result.Warnings);
        AddConsecutiveDutyWarnings(schedule, result.Warnings);
        AddMissingWeeklyDayOffWarnings(schedule, result.Warnings);

        result.ErrorCount = result.Warnings.Count(x => x.Severity == "Error");
        result.WarningCount = result.Warnings.Count(x => x.Severity == "Warning");

        return result;
    }

    private static void AddDutyConsistencyWarnings(
        PlanningSchedule schedule,
        ICollection<PlanningScheduleValidationWarningDto> warnings)
    {
        foreach (var assignment in schedule.Assignments)
        {
            if (assignment.AssignmentType == PlanningAssignmentType.Duty && !assignment.PlanningDutyId.HasValue)
            {
                warnings.Add(CreateWarning(
                    "Error",
                    assignment,
                    "DutyRequiresDutyId",
                    "Przypisanie typu Służba musi mieć wybraną służbę z biblioteki."));
            }

            if (assignment.AssignmentType != PlanningAssignmentType.Duty && assignment.PlanningDutyId.HasValue)
            {
                warnings.Add(CreateWarning(
                    "Error",
                    assignment,
                    "VacationOrSickWithDuty",
                    "Przypisanie inne niż Służba nie może mieć wybranej służby."));
            }
        }
    }

    private static void AddShortRestWarnings(
        PlanningSchedule schedule,
        ICollection<PlanningScheduleValidationWarningDto> warnings)
    {
        foreach (var driverGroup in schedule.Assignments
            .Where(IsDutyWithTime)
            .GroupBy(x => x.DriverId))
        {
            var duties = driverGroup
                .OrderBy(x => x.Date)
                .ThenBy(x => x.PlanningDuty!.StartTime)
                .ToList();

            for (var index = 1; index < duties.Count; index++)
            {
                var previousEnd = GetDutyEnd(duties[index - 1]);
                var currentStart = GetDutyStart(duties[index]);
                var rest = currentStart - previousEnd;

                if (rest.TotalHours < 11)
                {
                    warnings.Add(CreateWarning(
                        "Warning",
                        duties[index],
                        "ShortRestBetweenDuties",
                        $"Odpoczynek między służbami wynosi {Math.Max(0, rest.TotalHours):0.#} h, mniej niż 11 h."));
                }
            }
        }
    }

    private static void AddConsecutiveDutyWarnings(
        PlanningSchedule schedule,
        ICollection<PlanningScheduleValidationWarningDto> warnings)
    {
        foreach (var driverGroup in schedule.Assignments
            .Where(x => x.AssignmentType == PlanningAssignmentType.Duty)
            .GroupBy(x => x.DriverId))
        {
            var dutyDates = driverGroup
                .Select(x => x.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            var streak = 1;

            for (var index = 1; index < dutyDates.Count; index++)
            {
                streak = dutyDates[index - 1].AddDays(1) == dutyDates[index]
                    ? streak + 1
                    : 1;

                if (streak > 6)
                {
                    var assignment = driverGroup.First(x => x.Date == dutyDates[index]);
                    warnings.Add(CreateWarning(
                        "Warning",
                        assignment,
                        "TooManyConsecutiveDutyDays",
                        "Kierowca ma więcej niż 6 dni służb z rzędu."));
                    break;
                }
            }
        }
    }

    private static void AddMissingWeeklyDayOffWarnings(
        PlanningSchedule schedule,
        ICollection<PlanningScheduleValidationWarningDto> warnings)
    {
        foreach (var driverGroup in schedule.Assignments.GroupBy(x => x.DriverId))
        {
            foreach (var weekGroup in driverGroup.GroupBy(x => ISOWeek.GetWeekOfYear(x.Date.ToDateTime(TimeOnly.MinValue))))
            {
                if (weekGroup.Any(x => x.AssignmentType != PlanningAssignmentType.Duty))
                {
                    continue;
                }

                var firstAssignment = weekGroup.OrderBy(x => x.Date).First();
                warnings.Add(CreateWarning(
                    "Warning",
                    firstAssignment,
                    "MissingWeeklyDayOff",
                    "W tygodniu ISO brak dnia wolnego, urlopu, chorobowego, szkolenia albo innego dnia bez służby."));
            }
        }
    }

    private static bool IsDutyWithTime(PlanningAssignment assignment) =>
        assignment.AssignmentType == PlanningAssignmentType.Duty
        && assignment.PlanningDuty?.StartTime is not null
        && assignment.PlanningDuty.EndTime is not null;

    private static DateTime GetDutyStart(PlanningAssignment assignment) =>
        assignment.Date.ToDateTime(assignment.PlanningDuty!.StartTime!.Value);

    private static DateTime GetDutyEnd(PlanningAssignment assignment)
    {
        var start = GetDutyStart(assignment);
        var end = assignment.Date.ToDateTime(assignment.PlanningDuty!.EndTime!.Value);

        return end <= start ? end.AddDays(1) : end;
    }

    private static PlanningScheduleValidationWarningDto CreateWarning(
        string severity,
        PlanningAssignment assignment,
        string code,
        string message) => new()
    {
        Severity = severity,
        Date = assignment.Date,
        DriverId = assignment.DriverId,
        DriverName = FormatDriverName(assignment.Driver),
        AssignmentId = assignment.Id,
        Code = code,
        Message = message
    };

    private static string FormatDriverName(Driver driver)
    {
        var name = $"{driver.FirstName} {driver.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? driver.CardNumber : name;
    }
}
