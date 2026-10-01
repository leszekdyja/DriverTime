using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningScheduleServiceTests
{
    [TestMethod]
    public void CreateScheduleForCompany_AssignsCompanyId()
    {
        var companyId = Guid.NewGuid();

        var schedule = PlanningScheduleService.CreateScheduleForCompany(
            CreateScheduleRequest(),
            companyId,
            DateTime.UtcNow);

        Assert.AreEqual(companyId, schedule.CompanyId);
        Assert.AreEqual("Grafik czerwiec", schedule.Name);
        Assert.AreEqual(2026, schedule.Year);
        Assert.AreEqual(6, schedule.Month);
    }

    [TestMethod]
    public void UpsertAssignmentForCompany_CreatesDutyAssignment()
    {
        var companyId = Guid.NewGuid();
        var schedule = CreateSchedule(companyId);
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId);
        var assignments = new List<PlanningAssignment>();

        var assignment = PlanningScheduleService.UpsertAssignmentForCompany(
            assignments,
            CreateAssignmentRequest(driver.Id, duty.Id),
            schedule,
            driver,
            duty,
            companyId,
            DateTime.UtcNow);

        Assert.AreEqual(1, assignments.Count);
        Assert.AreEqual(PlanningAssignmentType.Duty, assignment.AssignmentType);
        Assert.AreEqual(duty.Id, assignment.PlanningDutyId);
    }

    [TestMethod]
    public void UpsertAssignmentForCompany_SecondUpsertSameDriverAndDate_UpdatesInsteadOfDuplicating()
    {
        var companyId = Guid.NewGuid();
        var schedule = CreateSchedule(companyId);
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId);
        var assignments = new List<PlanningAssignment>();
        var request = CreateAssignmentRequest(driver.Id, duty.Id);

        PlanningScheduleService.UpsertAssignmentForCompany(assignments, request, schedule, driver, duty, companyId, DateTime.UtcNow);
        request.Notes = "Zmieniona notatka";
        var updated = PlanningScheduleService.UpsertAssignmentForCompany(assignments, request, schedule, driver, duty, companyId, DateTime.UtcNow.AddMinutes(1));

        Assert.AreEqual(1, assignments.Count);
        Assert.AreEqual("Zmieniona notatka", updated.Notes);
        Assert.IsNotNull(updated.UpdatedUtc);
    }

    [TestMethod]
    public void UpsertAssignmentForCompany_RejectsDriverFromOtherCompany()
    {
        var companyId = Guid.NewGuid();
        var schedule = CreateSchedule(companyId);
        var driver = CreateDriver(Guid.NewGuid());
        var duty = CreateDuty(companyId);

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            PlanningScheduleService.UpsertAssignmentForCompany(
                new List<PlanningAssignment>(),
                CreateAssignmentRequest(driver.Id, duty.Id),
                schedule,
                driver,
                duty,
                companyId,
                DateTime.UtcNow));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("kierowcy spoza", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void UpsertAssignmentForCompany_RejectsDutyFromOtherCompany()
    {
        var companyId = Guid.NewGuid();
        var schedule = CreateSchedule(companyId);
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(Guid.NewGuid());

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            PlanningScheduleService.UpsertAssignmentForCompany(
                new List<PlanningAssignment>(),
                CreateAssignmentRequest(driver.Id, duty.Id),
                schedule,
                driver,
                duty,
                companyId,
                DateTime.UtcNow));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("służby spoza", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ValidateAssignmentRequest_DutyRequiresPlanningDutyId()
    {
        var request = CreateAssignmentRequest(Guid.NewGuid(), Guid.NewGuid());
        request.PlanningDutyId = null;

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            PlanningScheduleService.ValidateAssignmentRequest(request));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("wybierz służbę", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void DriverTimeModel_DeletesAssignmentsWithSchedule()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseNpgsql("Host=localhost;Database=drivertime;Username=drivetime;Password=postgres")
            .Options;
        using var dbContext = new DriverTimeDbContext(options);

        var assignmentForeignKey = dbContext.Model.FindEntityType(typeof(PlanningAssignment))
            ?.GetForeignKeys()
            .SingleOrDefault(x => x.PrincipalEntityType.ClrType == typeof(PlanningSchedule));

        Assert.IsNotNull(assignmentForeignKey);
        Assert.AreEqual(DeleteBehavior.Cascade, assignmentForeignKey.DeleteBehavior);
    }

    [TestMethod]
    public void IsAssignmentInCompanyScope_ReturnsTrueOnlyForCurrentCompanyAndSchedule()
    {
        var companyId = Guid.NewGuid();
        var schedule = CreateSchedule(companyId);
        var assignment = new PlanningAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanningScheduleId = schedule.Id,
            PlanningSchedule = schedule,
            DriverId = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 1)
        };

        Assert.IsTrue(PlanningScheduleService.IsAssignmentInCompanyScope(assignment, schedule.Id, assignment.Id, companyId));
        Assert.IsFalse(PlanningScheduleService.IsAssignmentInCompanyScope(assignment, schedule.Id, assignment.Id, Guid.NewGuid()));
        Assert.IsFalse(PlanningScheduleService.IsAssignmentInCompanyScope(assignment, Guid.NewGuid(), assignment.Id, companyId));
    }

    private static PlanningScheduleCreateRequestDto CreateScheduleRequest() => new()
    {
        Name = "Grafik czerwiec",
        Year = 2026,
        Month = 6,
        Notes = "Test"
    };

    private static PlanningAssignmentUpsertRequestDto CreateAssignmentRequest(Guid driverId, Guid dutyId) => new()
    {
        DriverId = driverId,
        PlanningDutyId = dutyId,
        Date = new DateOnly(2026, 6, 12),
        AssignmentType = "Duty",
        Notes = "Test"
    };

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

    private static PlanningDuty CreateDuty(Guid companyId)
    {
        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DutyNumber = "101",
            Name = "Służba 101",
            StartTime = new TimeOnly(5, 20),
            EndTime = new TimeOnly(13, 45)
        };
        duty.Lines.Add(new PlanningDutyLine
        {
            Id = Guid.NewGuid(),
            PlanningDutyId = duty.Id,
            LineCode = "K-11"
        });
        return duty;
    }
}
