using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningAutoGeneratorServiceTests
{
    [TestMethod]
    public void GenerateAssignmentForTest_AssignsDutyToDriver()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId, "Adam", "Nowak");
        var duty = CreateDuty(companyId, "1", new TimeOnly(6, 0), new TimeOnly(14, 0));
        var schedule = CreateSchedule(companyId);
        var nextDriverIndex = 0;

        var assignment = PlanningAutoGeneratorService.GenerateAssignmentForTest(
            new List<Driver> { driver },
            new Dictionary<Guid, List<AssignmentInterval>>(),
            new HashSet<(Guid DriverId, DateOnly Date)>(),
            duty,
            schedule,
            new DateOnly(2026, 8, 1),
            companyId,
            DateTime.UtcNow,
            ref nextDriverIndex);

        Assert.IsNotNull(assignment);
        Assert.AreEqual(driver.Id, assignment.DriverId);
        Assert.AreEqual(duty.Id, assignment.PlanningDutyId);
        Assert.AreEqual(PlanningAssignmentStatus.Generated, assignment.Status);
    }

    [TestMethod]
    public void GenerateAssignmentForTest_DoesNotCreateTimeConflict()
    {
        var companyId = Guid.NewGuid();
        var blockedDriver = CreateDriver(companyId, "Adam", "Nowak");
        var freeDriver = CreateDriver(companyId, "Beata", "Kowalska");
        var duty = CreateDuty(companyId, "2", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var schedule = CreateSchedule(companyId);
        var date = new DateOnly(2026, 8, 1);
        var nextDriverIndex = 0;
        var intervals = new Dictionary<Guid, List<AssignmentInterval>>
        {
            [blockedDriver.Id] = new() { new AssignmentInterval(date.ToDateTime(new TimeOnly(7, 0)), date.ToDateTime(new TimeOnly(12, 0))) }
        };

        var assignment = PlanningAutoGeneratorService.GenerateAssignmentForTest(
            new List<Driver> { blockedDriver, freeDriver },
            intervals,
            new HashSet<(Guid DriverId, DateOnly Date)>(),
            duty,
            schedule,
            date,
            companyId,
            DateTime.UtcNow,
            ref nextDriverIndex);

        Assert.IsNotNull(assignment);
        Assert.AreEqual(freeDriver.Id, assignment.DriverId);
        Assert.AreEqual(1, intervals[blockedDriver.Id].Count);
    }

    [TestMethod]
    public void BuildInterval_DutyCrossingMidnight_EndsNextDay()
    {
        var duty = CreateDuty(Guid.NewGuid(), "RN", new TimeOnly(20, 20), new TimeOnly(7, 20));

        var interval = PlanningAutoGeneratorService.BuildInterval(new DateOnly(2026, 8, 2), duty);

        Assert.IsNotNull(interval);
        Assert.AreEqual(new DateTime(2026, 8, 2, 20, 20, 0), interval.Value.Start);
        Assert.AreEqual(new DateTime(2026, 8, 3, 7, 20, 0), interval.Value.End);
    }

    [TestMethod]
    public void GenerateAssignmentForTest_DoesNotUseDriverWithManualAssignmentOnThatDay()
    {
        var companyId = Guid.NewGuid();
        var manualDriver = CreateDriver(companyId, "Adam", "Nowak");
        var freeDriver = CreateDriver(companyId, "Beata", "Kowalska");
        var duty = CreateDuty(companyId, "3", new TimeOnly(6, 0), new TimeOnly(14, 0));
        var schedule = CreateSchedule(companyId);
        var date = new DateOnly(2026, 8, 1);
        var nextDriverIndex = 0;

        var assignment = PlanningAutoGeneratorService.GenerateAssignmentForTest(
            new List<Driver> { manualDriver, freeDriver },
            new Dictionary<Guid, List<AssignmentInterval>>(),
            new HashSet<(Guid DriverId, DateOnly Date)> { (manualDriver.Id, date) },
            duty,
            schedule,
            date,
            companyId,
            DateTime.UtcNow,
            ref nextDriverIndex);

        Assert.IsNotNull(assignment);
        Assert.AreEqual(freeDriver.Id, assignment.DriverId);
    }


    [TestMethod]
    public void GenerateAssignmentForTest_DoesNotUseDriverUnavailableOnThatDay()
    {
        var companyId = Guid.NewGuid();
        var unavailableDriver = CreateDriver(companyId, "Adam", "Urlopowy");
        var freeDriver = CreateDriver(companyId, "Beata", "Dostępna");
        var duty = CreateDuty(companyId, "4", new TimeOnly(7, 0), new TimeOnly(15, 0));
        var schedule = CreateSchedule(companyId);
        var date = new DateOnly(2026, 8, 3);
        var nextDriverIndex = 0;

        var assignment = PlanningAutoGeneratorService.GenerateAssignmentForTest(
            new List<Driver> { unavailableDriver, freeDriver },
            new Dictionary<Guid, List<AssignmentInterval>>(),
            new HashSet<(Guid DriverId, DateOnly Date)> { (unavailableDriver.Id, date) },
            duty,
            schedule,
            date,
            companyId,
            DateTime.UtcNow,
            ref nextDriverIndex);

        Assert.IsNotNull(assignment);
        Assert.AreEqual(freeDriver.Id, assignment.DriverId);
    }
    private static Driver CreateDriver(Guid companyId, string firstName, string lastName) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        FirstName = firstName,
        LastName = lastName,
        CardNumber = Guid.NewGuid().ToString("N")
    };

    private static PlanningDuty CreateDuty(Guid companyId, string dutyNumber, TimeOnly start, TimeOnly end) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        DutyNumber = dutyNumber,
        Name = $"Służba {dutyNumber}",
        StartTime = start,
        EndTime = end
    };

    private static PlanningSchedule CreateSchedule(Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Name = "Grafik testowy",
        Year = 2026,
        Month = 8
    };
}


