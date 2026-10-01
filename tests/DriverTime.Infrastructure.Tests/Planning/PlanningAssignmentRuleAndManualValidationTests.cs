using DriverTime.Application.Planning;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningAssignmentRuleAndManualValidationTests
{
    [TestMethod]
    public void ValidateManualAssignment_ForbiddenDutyRuleRejectsDriver()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId, "12", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var service = new PlanningAssignmentValidationService();

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            service.ValidateManualAssignment(new PlanningAssignmentValidationRequest
            {
                CompanyId = companyId,
                Driver = driver,
                PlanningDuty = duty,
                Date = new DateOnly(2026, 8, 10),
                AssignmentRules = new[]
                {
                    new PlanningAssignmentRule
                    {
                        CompanyId = companyId,
                        DriverId = driver.Id,
                        DutyId = duty.Id,
                        Type = PlanningAssignmentRuleType.Forbidden
                    }
                }
            }));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("zakaz", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ValidateManualAssignment_PreferredRuleDoesNotBlockDriver()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId, "12", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var service = new PlanningAssignmentValidationService();

        service.ValidateManualAssignment(new PlanningAssignmentValidationRequest
        {
            CompanyId = companyId,
            Driver = driver,
            PlanningDuty = duty,
            Date = new DateOnly(2026, 8, 10),
            AssignmentRules = new[]
            {
                new PlanningAssignmentRule
                {
                    CompanyId = companyId,
                    DriverId = driver.Id,
                    DutyId = duty.Id,
                    Type = PlanningAssignmentRuleType.Preferred
                }
            }
        });
    }

    [TestMethod]
    public void ValidateManualAssignment_AvailabilityRejectsManualEntry()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId, "12", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var service = new PlanningAssignmentValidationService();

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            service.ValidateManualAssignment(new PlanningAssignmentValidationRequest
            {
                CompanyId = companyId,
                Driver = driver,
                PlanningDuty = duty,
                Date = new DateOnly(2026, 8, 10),
                Availabilities = new[]
                {
                    new PlanningDriverAvailability
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        DriverId = driver.Id,
                        DateFrom = new DateOnly(2026, 8, 9),
                        DateTo = new DateOnly(2026, 8, 11),
                        Type = PlanningDriverAvailabilityType.Vacation
                    }
                }
            }));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("urlop", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ValidateManualAssignment_UpdateIgnoresCurrentAssignmentButKeepsOtherSameDayBlocker()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId, "12", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var date = new DateOnly(2026, 8, 10);
        var current = CreateAssignment(companyId, driver.Id, date, duty);
        var other = CreateAssignment(companyId, driver.Id, date, duty);
        var service = new PlanningAssignmentValidationService();

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            service.ValidateManualAssignment(new PlanningAssignmentValidationRequest
            {
                CompanyId = companyId,
                AssignmentId = current.Id,
                Driver = driver,
                PlanningDuty = duty,
                Date = date,
                ExistingAssignments = new[] { current, other }
            }));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("już wpis", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ValidateManualAssignment_PreviousDutyWithShortRestRejectsManualEntry()
    {
        var companyId = Guid.NewGuid();
        var driver = CreateDriver(companyId);
        var duty = CreateDuty(companyId, "12", new TimeOnly(12, 0), new TimeOnly(20, 0));
        var previousDuty = CreateDuty(companyId, "RN", new TimeOnly(20, 0), new TimeOnly(6, 0));
        var previous = CreateAssignment(
            companyId,
            driver.Id,
            new DateOnly(2026, 8, 9),
            previousDuty,
            new DateTime(2026, 8, 9, 20, 0, 0),
            new DateTime(2026, 8, 10, 6, 0, 0));
        var service = new PlanningAssignmentValidationService();

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            service.ValidateManualAssignment(new PlanningAssignmentValidationRequest
            {
                CompanyId = companyId,
                Driver = driver,
                PlanningDuty = duty,
                Date = new DateOnly(2026, 8, 10),
                ExistingAssignments = new[] { previous },
                MinDailyRestMinutes = 9 * 60
            }));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("odpoczynek", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void DriverTimeModel_PlanningDriverDutyRuleHasUniqueDuplicatePreventionIndex()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseNpgsql("Host=localhost;Database=drivertime;Username=drivetime;Password=postgres")
            .Options;
        using var dbContext = new DriverTimeDbContext(options);

        var entityType = dbContext.Model.FindEntityType(typeof(PlanningDriverDutyRule));
        Assert.IsNotNull(entityType);

        var uniqueIndex = entityType.GetIndexes().SingleOrDefault(x =>
            x.IsUnique
            && x.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(PlanningDriverDutyRule.CompanyId),
                nameof(PlanningDriverDutyRule.DriverId),
                nameof(PlanningDriverDutyRule.PlanningDutyId),
                nameof(PlanningDriverDutyRule.Type),
                nameof(PlanningDriverDutyRule.ValidFrom),
                nameof(PlanningDriverDutyRule.ValidTo)
            }));

        Assert.IsNotNull(uniqueIndex);
    }

    private static Driver CreateDriver(Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        FirstName = "Adam",
        LastName = "Nowak",
        CardNumber = Guid.NewGuid().ToString("N")
    };

    private static PlanningDuty CreateDuty(Guid companyId, string dutyNumber, TimeOnly start, TimeOnly end) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        DutyNumber = dutyNumber,
        Name = $"Służba {dutyNumber}",
        StartTime = start,
        EndTime = end,
        WorkMinutes = 480
    };

    private static PlanningAssignment CreateAssignment(
        Guid companyId,
        Guid driverId,
        DateOnly date,
        PlanningDuty duty,
        DateTime? start = null,
        DateTime? end = null) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        PlanningScheduleId = Guid.NewGuid(),
        DriverId = driverId,
        Date = date,
        PlanningDutyId = duty.Id,
        PlanningDuty = duty,
        StartDateTime = start ?? date.ToDateTime(duty.StartTime!.Value),
        EndDateTime = end ?? date.ToDateTime(duty.EndTime!.Value),
        Status = PlanningAssignmentStatus.Manual,
        AssignmentType = PlanningAssignmentType.Duty
    };
}

