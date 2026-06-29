using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningScheduleValidationServiceTests
{
    [TestMethod]
    public void Validate_ShortRestBetweenDuties_ReturnsWarning()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var schedule = CreateSchedule(companyId);
        var firstDuty = CreateDuty(companyId, "101", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var secondDuty = CreateDuty(companyId, "102", new TimeOnly(2, 0), new TimeOnly(10, 0));
        schedule.Assignments.Add(CreateAssignment(companyId, schedule, driver, new DateOnly(2026, 6, 1), PlanningAssignmentType.Duty, firstDuty));
        schedule.Assignments.Add(CreateAssignment(companyId, schedule, driver, new DateOnly(2026, 6, 2), PlanningAssignmentType.Duty, secondDuty));

        var result = PlanningScheduleValidationService.Validate(schedule);

        Assert.IsTrue(result.Warnings.Any(x => x.Code == "ShortRestBetweenDuties" && x.Severity == "Warning"));
    }

    [TestMethod]
    public void Validate_SevenConsecutiveDutyDays_ReturnsWarning()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var schedule = CreateSchedule(companyId);
        var duty = CreateDuty(companyId, "101", new TimeOnly(8, 0), new TimeOnly(16, 0));

        for (var day = 1; day <= 7; day++)
        {
            schedule.Assignments.Add(CreateAssignment(companyId, schedule, driver, new DateOnly(2026, 6, day), PlanningAssignmentType.Duty, duty));
        }

        var result = PlanningScheduleValidationService.Validate(schedule);

        Assert.IsTrue(result.Warnings.Any(x => x.Code == "TooManyConsecutiveDutyDays" && x.Severity == "Warning"));
    }

    [TestMethod]
    public void Validate_DutyWithoutPlanningDutyId_ReturnsError()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var schedule = CreateSchedule(companyId);
        schedule.Assignments.Add(CreateAssignment(companyId, schedule, driver, new DateOnly(2026, 6, 1), PlanningAssignmentType.Duty, null));

        var result = PlanningScheduleValidationService.Validate(schedule);

        Assert.IsTrue(result.Warnings.Any(x => x.Code == "DutyRequiresDutyId" && x.Severity == "Error"));
        Assert.AreEqual(1, result.ErrorCount);
    }

    [TestMethod]
    public void Validate_NonDutyWithPlanningDutyId_ReturnsError()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var schedule = CreateSchedule(companyId);
        var duty = CreateDuty(companyId, "101", new TimeOnly(8, 0), new TimeOnly(16, 0));
        schedule.Assignments.Add(CreateAssignment(companyId, schedule, driver, new DateOnly(2026, 6, 1), PlanningAssignmentType.Vacation, duty));

        var result = PlanningScheduleValidationService.Validate(schedule);

        Assert.IsTrue(result.Warnings.Any(x => x.Code == "VacationOrSickWithDuty" && x.Severity == "Error"));
        Assert.AreEqual(1, result.ErrorCount);
    }

    [TestMethod]
    public void Validate_MissingWeeklyDayOff_ReturnsWarning()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var schedule = CreateSchedule(companyId);
        var duty = CreateDuty(companyId, "101", new TimeOnly(8, 0), new TimeOnly(16, 0));

        for (var day = 1; day <= 5; day++)
        {
            schedule.Assignments.Add(CreateAssignment(companyId, schedule, driver, new DateOnly(2026, 6, day), PlanningAssignmentType.Duty, duty));
        }

        var result = PlanningScheduleValidationService.Validate(schedule);

        Assert.IsTrue(result.Warnings.Any(x => x.Code == "MissingWeeklyDayOff" && x.Severity == "Warning"));
    }

    private static PlanningSchedule CreateSchedule(Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Name = "Grafik czerwiec",
        Year = 2026,
        Month = 6
    };

    private static Driver CreateDriver(Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        FirstName = "Jan",
        LastName = "Kowalski",
        CardNumber = "123"
    };

    private static PlanningDuty CreateDuty(Guid companyId, string number, TimeOnly start, TimeOnly end) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        DutyNumber = number,
        Name = $"Służba {number}",
        StartTime = start,
        EndTime = end
    };

    private static PlanningAssignment CreateAssignment(
        Guid companyId,
        PlanningSchedule schedule,
        Driver driver,
        DateOnly date,
        PlanningAssignmentType assignmentType,
        PlanningDuty? duty) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        PlanningScheduleId = schedule.Id,
        PlanningSchedule = schedule,
        DriverId = driver.Id,
        Driver = driver,
        PlanningDutyId = duty?.Id,
        PlanningDuty = duty,
        Date = date,
        AssignmentType = assignmentType
    };
}
