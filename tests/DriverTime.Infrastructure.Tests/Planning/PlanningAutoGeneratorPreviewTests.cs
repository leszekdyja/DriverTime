using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningAutoGeneratorPreviewTests
{
    [TestMethod]
    public async Task PreviewAsync_ReturnsAssignmentsWithoutPersistingChanges()
    {
        var companyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DriverTimeDbContext(options);
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Adam",
            LastName = "Nowak",
            CardNumber = "CARD-1",
            IncludeInPlanning = true
        };
        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DutyNumber = "1",
            Name = "Służba 1",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            WorkMinutes = 480
        };
        var existingSchedule = new PlanningSchedule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Istniejący grafik",
            Year = 2026,
            Month = 8,
            CreatedUtc = DateTime.UtcNow
        };
        var existingAssignment = new PlanningAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanningScheduleId = existingSchedule.Id,
            DriverId = driver.Id,
            PlanningDutyId = duty.Id,
            Date = new DateOnly(2026, 8, 3),
            StartDateTime = new DateTime(2026, 8, 3, 8, 0, 0),
            EndDateTime = new DateTime(2026, 8, 3, 16, 0, 0),
            Status = PlanningAssignmentStatus.Generated,
            AssignmentType = PlanningAssignmentType.Duty,
            CreatedUtc = DateTime.UtcNow
        };
        dbContext.Drivers.Add(driver);
        dbContext.PlanningDuties.Add(duty);
        dbContext.PlanningSchedules.Add(existingSchedule);
        dbContext.PlanningAssignments.Add(existingAssignment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new PlanningAutoGeneratorService(dbContext, new TestCurrentUserService(companyId));
        var request = new PlanningAutoGenerateRequestDto
        {
            DateFrom = new DateOnly(2026, 8, 3),
            DateTo = new DateOnly(2026, 8, 3)
        };

        var result = await service.PreviewAsync(request);

        Assert.IsTrue(result.IsPreview);
        Assert.AreEqual(1, result.GeneratedCount);
        Assert.AreEqual(1, result.ProposedAssignments.Count);
        Assert.AreEqual(driver.Id, result.ProposedAssignments[0].DriverId);
        Assert.AreEqual(duty.Id, result.ProposedAssignments[0].PlanningDutyId);
        Assert.AreNotEqual(existingAssignment.Id, result.ProposedAssignments[0].Id);
        var persistedAssignment = await dbContext.PlanningAssignments.AsNoTracking().SingleAsync();
        Assert.AreEqual(existingAssignment.Id, persistedAssignment.Id);
        Assert.AreEqual(existingSchedule.Id, persistedAssignment.PlanningScheduleId);
        Assert.AreEqual(1, await dbContext.PlanningSchedules.CountAsync());
        Assert.AreEqual(0, dbContext.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task GenerateAsync_PersistsTheSameProposedAssignments()
    {
        var companyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new DriverTimeDbContext(options);
        dbContext.Drivers.Add(new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Ewa",
            LastName = "Kowalska",
            CardNumber = "CARD-2",
            IncludeInPlanning = true
        });
        dbContext.PlanningDuties.Add(new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DutyNumber = "2",
            Name = "Służba 2",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            WorkMinutes = 480
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new PlanningAutoGeneratorService(dbContext, new TestCurrentUserService(companyId));
        var result = await service.GenerateAsync(new PlanningAutoGenerateRequestDto
        {
            DateFrom = new DateOnly(2026, 8, 3),
            DateTo = new DateOnly(2026, 8, 3)
        });

        Assert.IsFalse(result.IsPreview);
        Assert.AreEqual(result.GeneratedCount, result.ProposedAssignments.Count);
        Assert.AreEqual(result.GeneratedCount, await dbContext.PlanningAssignments.CountAsync());
        Assert.AreEqual(1, await dbContext.PlanningSchedules.CountAsync());
    }

    private sealed class TestCurrentUserService(Guid companyId) : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();

        public Guid CompanyId { get; } = companyId;

        public bool IsAuthenticated => true;
    }
}