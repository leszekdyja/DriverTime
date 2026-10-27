using System.Text.Json;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class LocalPlanningImportTests
{
    [TestMethod]
    public async Task PrepareDoesNotWriteAndConfirmPreservesManualProvenanceAndOvernightTimes()
    {
        using var f = new Fixture();
        var request = f.Request();
        var plan = await f.Service.PrepareAsync(request, default);
        Assert.IsTrue(plan.CanApply);
        Assert.AreEqual(0, await f.Db.PlanningAssignments.CountAsync());
        Assert.AreEqual(0, await f.Db.LocalPlanningSyncBatches.CountAsync());
        request.RevisionToken = plan.RevisionToken;
        var result = await f.Service.ApplyAsync(request, default);
        Assert.AreEqual(1, result.Created);
        var saved = await f.Db.PlanningAssignments.SingleAsync();
        Assert.AreEqual(PlanningAssignmentStatus.Manual, saved.Status);
        Assert.AreEqual(new DateTime(2026, 10, 31, 20, 0, 0), saved.StartDateTime);
        Assert.AreEqual(new DateTime(2026, 11, 1, 6, 0, 0), saved.EndDateTime);
        Assert.AreEqual(1, await f.Db.LocalPlanningSyncBatches.CountAsync());
    }

    [TestMethod]
    public async Task NetworkRetryAndRepeatedFileDoNotDuplicateSchedulesOrCells()
    {
        using var f = new Fixture();
        var request = f.Request();
        var first = await f.Apply(request);
        var retried = await f.Service.ApplyAsync(request, default);
        Assert.AreEqual(first, retried);
        request.OperationId = Guid.NewGuid();
        var second = await f.Apply(request);
        Assert.AreEqual(first.ScheduleId, second.ScheduleId);
        Assert.AreEqual(0, second.Created);
        Assert.AreEqual(1, second.Unchanged);
        Assert.AreEqual(1, await f.Db.PlanningSchedules.CountAsync());
        Assert.AreEqual(1, await f.Db.PlanningAssignments.CountAsync());
    }

    [TestMethod]
    public async Task ManualConflictRequiresAnExplicitDecisionAndKeepsServerWhenSelected()
    {
        using var f = new Fixture();
        var current = await f.Existing(PlanningAssignmentStatus.Manual);
        var request = f.Request(current.PlanningScheduleId);
        var plan = await f.Service.PrepareAsync(request, default);
        Assert.AreEqual(1, plan.Conflicts.Count);
        Assert.IsFalse(plan.CanApply);
        request.RevisionToken = plan.RevisionToken;
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Service.ApplyAsync(request, default));
        request.Decisions[3] = "KeepServer";
        var result = await f.Apply(request);
        Assert.AreEqual(1, result.KeptServer);
        Assert.AreEqual("stary wpis", current.Notes);
    }

    [TestMethod]
    public async Task ExplicitOverwriteHasBackupAndCanBeReverted()
    {
        using var f = new Fixture();
        var current = await f.Existing(PlanningAssignmentStatus.Generated);
        var oldId = current.Id; var oldDuty = current.PlanningDutyId;
        var request = f.Request(current.PlanningScheduleId);
        request.Decisions[3] = "UseLocal";
        var result = await f.Apply(request);
        Assert.AreEqual(1, result.Updated);
        Assert.AreEqual(f.Duty.Id, current.PlanningDutyId);
        var batch = await f.Db.LocalPlanningSyncBatches.SingleAsync();
        Assert.IsTrue(batch.BackupJson.Contains("stary wpis"));
        await f.Service.RevertAsync(result.BatchId, default);
        Assert.AreEqual(oldId, current.Id);
        Assert.AreEqual(oldDuty, current.PlanningDutyId);
        Assert.AreEqual("stary wpis", current.Notes);
        Assert.AreEqual(PlanningAssignmentStatus.Generated, current.Status);
        await f.Service.RevertAsync(result.BatchId, default);
        Assert.IsNotNull(batch.RevertedAtUtc);
    }

    [TestMethod]
    public async Task UndoRemovesOnlyImportedCellsAndRefusesLaterEdits()
    {
        using var f = new Fixture();
        var first = await f.Apply(f.Request());
        var cell = await f.Db.PlanningAssignments.SingleAsync();
        cell.Notes = "późniejsza edycja";
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Service.RevertAsync(first.BatchId, default));
        Assert.AreEqual(1, await f.Db.PlanningAssignments.CountAsync());
        cell.Notes = null;
        await f.Db.SaveChangesAsync();
        await f.Service.RevertAsync(first.BatchId, default);
        Assert.AreEqual(0, await f.Db.PlanningAssignments.CountAsync());
        Assert.AreEqual(1, await f.Db.PlanningSchedules.CountAsync());
    }

    [TestMethod]
    public async Task ChangedGraphAfterPreviewInvalidatesToken()
    {
        using var f = new Fixture();
        var current = await f.Existing(PlanningAssignmentStatus.Generated);
        var request = f.Request(current.PlanningScheduleId);
        request.Decisions[3] = "UseLocal";
        request.RevisionToken = (await f.Service.PrepareAsync(request, default)).RevisionToken;
        current.Notes = "zmiana po podglądzie";
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Service.ApplyAsync(request, default));
        Assert.AreEqual(0, await f.Db.LocalPlanningSyncBatches.CountAsync());
    }

    [TestMethod]
    public async Task SkippingAndDefinitionSemanticsNeedSeparateAcknowledgements()
    {
        using var f = new Fixture();
        var request = f.Request();
        request.Snapshot.Tables.Entries.Add(new() { Id = 4, DriverId = 1, DutyId = 5, Date = new(2026, 10, 30), EntrySource = "auto" });
        request.Snapshot.Tables.Duties.Add(new() { Id = 5, Code = "brak" });
        var plan = await f.Service.PrepareAsync(request, default);
        Assert.AreEqual(1, plan.SkippedEntries);
        Assert.IsFalse(plan.CanApply);
        request.AllowSkipping = true; request.AcknowledgeDefinitions = false;
        Assert.IsFalse((await f.Service.PrepareAsync(request, default)).CanApply);
        request.AcknowledgeDefinitions = true;
        var result = await f.Apply(request);
        Assert.AreEqual(1, result.Created);
        Assert.AreEqual(1, result.Skipped);
    }

    [TestMethod]
    public async Task CrossTenantMappingsSchedulesAndUndoAreRejected()
    {
        using var f = new Fixture();
        var other = new Driver { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), LastName = "Obcy" };
        f.Db.Drivers.Add(other); await f.Db.SaveChangesAsync();
        var request = f.Request(); request.DriverMappings[1] = other.Id;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => f.Service.PrepareAsync(request, default));
        var result = await f.Apply(f.Request());
        var stranger = new LocalPlanningImportService(f.Db, new TestUser(other.CompanyId));
        Assert.AreEqual(0, (await stranger.GetBatchesAsync(default)).Count);
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => stranger.RevertAsync(result.BatchId, default));
        request = f.Request(result.ScheduleId); request.DriverMappings[1] = other.Id;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => stranger.PrepareAsync(request, default));
    }

    [TestMethod]
    public async Task CompanyAccountCannotImportUnassignedDriversOrSelectAnotherCompany()
    {
        using var f = new Fixture();
        var operating = new OperatingCompany { Id = Guid.NewGuid(), CompanyId = f.Company, Name = "Firma" };
        f.Db.OperatingCompanies.Add(operating); await f.Db.SaveChangesAsync();
        var service = new LocalPlanningImportService(f.Db, new TestUser(f.Company, operating.Id));
        var request = f.Request(); request.OperatingCompanyId = operating.Id;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => service.PrepareAsync(request, default));
        request.OperatingCompanyId = Guid.NewGuid();
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => service.PrepareAsync(request, default));
    }

    [TestMethod]
    public async Task DuplicateDriverMappingsAndMismatchedMonthAreRejected()
    {
        using var f = new Fixture();
        var request = f.Request();
        request.Snapshot.Tables.Drivers.Add(new() { Id = 9, Name = "Drugi" }); request.DriverMappings[9] = f.Driver.Id;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => f.Service.PrepareAsync(request, default));
        request = f.Request(); request.Month = 9;
        await Assert.ThrowsExceptionAsync<ArgumentException>(() => f.Service.PrepareAsync(request, default));
    }

    [TestMethod]
    public async Task ChangingDutyDefinitionInvalidatesPreparedImport()
    {
        using var f = new Fixture();
        var request = f.Request();
        request.RevisionToken = (await f.Service.PrepareAsync(request, default)).RevisionToken;
        f.Duty.StartTime = new TimeOnly(21, 0); await f.Db.SaveChangesAsync();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => f.Service.ApplyAsync(request, default));
    }

    [TestMethod]
    public async Task ImportDoesNotDeleteUnmentionedCellsOrCreateDriversAndDuties()
    {
        using var f = new Fixture();
        var current = await f.Existing(PlanningAssignmentStatus.Manual);
        current.Date = new(2026, 10, 15); await f.Db.SaveChangesAsync();
        var request = f.Request(current.PlanningScheduleId);
        await f.Apply(request);
        Assert.AreEqual(2, await f.Db.PlanningAssignments.CountAsync());
        Assert.AreEqual("stary wpis", current.Notes);
        Assert.AreEqual(1, await f.Db.Drivers.CountAsync());
        Assert.AreEqual(2, await f.Db.PlanningDuties.CountAsync());
    }

    private sealed class Fixture : IDisposable
    {
        public readonly DriverTimeDbContext Db = new(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Guid Company = Guid.NewGuid();
        private readonly Guid source = Guid.NewGuid();
        public readonly Driver Driver;
        public readonly PlanningDuty Duty;
        public LocalPlanningImportService Service => new(Db, new TestUser(Company));
        public Fixture()
        {
            Driver = new() { Id = Guid.NewGuid(), CompanyId = Company, FirstName = "Jan", LastName = "Test" };
            Duty = new() { Id = Guid.NewGuid(), CompanyId = Company, DutyNumber = "RN", StartTime = new(20, 0), EndTime = new(6, 0), WorkMinutes = 480 };
            Db.Drivers.Add(Driver); Db.PlanningDuties.Add(Duty); Db.SaveChanges();
        }
        public LocalPlanningImportRequestDto Request(Guid? schedule = null) => new()
        {
            OperationId = Guid.NewGuid(), Year = 2026, Month = 10, ScheduleId = schedule,
            AcknowledgeDefinitions = true, DriverMappings = new() { [1] = Driver.Id }, DutyMappings = new() { [2] = Duty.Id },
            Snapshot = new() { SchemaVersion = 1, SourceId = source, ExportedAtUtc = DateTime.UtcNow,
                Tables = new() { Drivers = new() { new() { Id = 1, Name = "Test" } }, Duties = new() { new() { Id = 2, Code = "RN" } },
                    Entries = new() { new() { Id = 3, DriverId = 1, DutyId = 2, Date = new(2026, 10, 31), EntrySource = "manual" } } } }
        };
        public async Task<LocalPlanningImportResultDto> Apply(LocalPlanningImportRequestDto request)
        {
            request.RevisionToken = (await Service.PrepareAsync(request, default)).RevisionToken;
            return await Service.ApplyAsync(request, default);
        }
        public async Task<PlanningAssignment> Existing(PlanningAssignmentStatus status)
        {
            var schedule = new PlanningSchedule { Id = Guid.NewGuid(), CompanyId = Company, Year = 2026, Month = 10 };
            var oldDuty = new PlanningDuty { Id = Guid.NewGuid(), CompanyId = Company, DutyNumber = "R" };
            Db.PlanningSchedules.Add(schedule); Db.PlanningDuties.Add(oldDuty);
            var assignment = new PlanningAssignment { Id = Guid.NewGuid(), CompanyId = Company, DriverId = Driver.Id,
                PlanningScheduleId = schedule.Id, PlanningDutyId = oldDuty.Id, Date = new(2026, 10, 31), Notes = "stary wpis", Status = status };
            Db.PlanningAssignments.Add(assignment); await Db.SaveChangesAsync();
            return assignment;
        }
        public void Dispose() => Db.Dispose();
    }
    private sealed class TestUser(Guid company, Guid? operating = null) : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public Guid CompanyId => company;
        public Guid? OperatingCompanyId => operating;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => true;
    }
}
