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
public class LocalPlanningSyncPreviewTests
{
    private static LocalPlanningSnapshotDto Snapshot() => new()
    {
        SchemaVersion = 1, SourceId = Guid.NewGuid(), ExportedAtUtc = DateTime.UtcNow,
        Tables = new()
        {
            Drivers = new() { new() { Id = 1, Name = "Łukasiewicz" } },
            Duties = new() { new() { Id = 2, Code = "RN" } },
            Entries = new() { new() { Id = 3, DriverId = 1, DutyId = 2, Date = new(2026, 10, 31), EntrySource = "manual" } }
        }
    };

    [TestMethod]
    public void JsonSnapshotPreservesUnknownFieldsAndSettings()
    {
        var snapshot = JsonSerializer.Deserialize<LocalPlanningSnapshotDto>("""
            {"schemaVersion":1,"sourceId":"bb13d0f2-e40a-415d-a96e-10f23057002f","exportedAtUtc":"2026-10-27T12:00:00Z",
             "tables":{"drivers":[{"id":1,"name":"Łukasiewicz","employee_no":"012"}],
             "duties":[{"id":2,"code":"RN","work_hours":8.5}],
             "planned_schedule":[{"id":3,"driver_id":1,"duty_id":2,"plan_date":"2026-10-31","entry_source":"manual","car_id":7}],
             "driver_day_plan_exclusions":[{"driver_id":1,"no_sundays":1}]}}
            """, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        LocalPlanningSyncPreviewService.Validate(snapshot);
        Assert.AreEqual("012", snapshot.Tables.Drivers[0].Details["employee_no"].GetString());
        Assert.AreEqual(7, snapshot.Tables.Entries[0].Details["car_id"].GetInt32());
        Assert.IsTrue(snapshot.Tables.Settings.ContainsKey("driver_day_plan_exclusions"));
    }

    [TestMethod]
    public void UniqueSurnameIsOnlySuggestedAndAmbiguityIsNotResolvedAutomatically()
    {
        var snapshot = Snapshot();
        var first = new Driver { Id = Guid.NewGuid(), FirstName = "Marek Janusz", LastName = "Łukasiewicz" };
        var preview = LocalPlanningSyncPreviewService.BuildPreview(snapshot, new[] { first }, Array.Empty<PlanningDuty>(), Array.Empty<PlanningAssignment>());
        Assert.AreEqual("Suggested", preview.DriverSuggestions[0].Status);
        Assert.IsTrue(preview.IsReadOnly);
        var second = new Driver { Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Łukasiewicz" };
        preview = LocalPlanningSyncPreviewService.BuildPreview(snapshot, new[] { first, second }, Array.Empty<PlanningDuty>(), Array.Empty<PlanningAssignment>());
        Assert.AreEqual("Ambiguous", preview.DriverSuggestions[0].Status);
        Assert.AreEqual(2, preview.DriverSuggestions[0].CandidateIds.Count);
    }

    [TestMethod]
    public void DuplicateDutyVersionsAndUnknownDriversRemainUnresolved()
    {
        var preview = LocalPlanningSyncPreviewService.BuildPreview(Snapshot(), Array.Empty<Driver>(),
            new[] { new PlanningDuty { Id = Guid.NewGuid(), DutyNumber = "RN" }, new PlanningDuty { Id = Guid.NewGuid(), DutyNumber = "RN" } }, Array.Empty<PlanningAssignment>());
        Assert.AreEqual("Missing", preview.DriverSuggestions[0].Status);
        Assert.AreEqual("Ambiguous", preview.DutySuggestions[0].Status);
    }

    [TestMethod]
    public void RejectsDuplicateDriverDaysAndBrokenReferences()
    {
        var snapshot = Snapshot();
        snapshot.Tables.Entries.Add(new() { Id = 4, DriverId = 1, DutyId = 2, Date = new(2026, 10, 31), EntrySource = "auto" });
        Assert.ThrowsException<ArgumentException>(() => LocalPlanningSyncPreviewService.Validate(snapshot));
        snapshot.Tables.Entries.RemoveAt(1);
        snapshot.Tables.Entries[0].DriverId = 999;
        Assert.ThrowsException<ArgumentException>(() => LocalPlanningSyncPreviewService.Validate(snapshot));
    }

    [TestMethod]
    public void RejectsUnsupportedVersionEmptyIdentityAndUnknownEntrySource()
    {
        var snapshot = Snapshot();
        snapshot.SchemaVersion = 2;
        Assert.ThrowsException<ArgumentException>(() => LocalPlanningSyncPreviewService.Validate(snapshot));
        snapshot.SchemaVersion = 1; snapshot.SourceId = Guid.Empty;
        Assert.ThrowsException<ArgumentException>(() => LocalPlanningSyncPreviewService.Validate(snapshot));
        snapshot.SourceId = Guid.NewGuid(); snapshot.Tables.Entries[0].EntrySource = "other";
        Assert.ThrowsException<ArgumentException>(() => LocalPlanningSyncPreviewService.Validate(snapshot));
    }

    [TestMethod]
    public async Task CompanyScopedPreviewNeverLeaksOtherCompanyOrUnassignedDriversAndDoesNotWrite()
    {
        using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid(); var operating = Guid.NewGuid();
        var own = new Driver { Id = Guid.NewGuid(), CompanyId = tenant, OperatingCompanyId = operating, LastName = "Łukasiewicz" };
        var other = new Driver { Id = Guid.NewGuid(), CompanyId = tenant, OperatingCompanyId = Guid.NewGuid(), LastName = "Łukasiewicz" };
        var unassigned = new Driver { Id = Guid.NewGuid(), CompanyId = tenant, LastName = "Łukasiewicz" };
        var otherTenant = new Driver { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), OperatingCompanyId = operating, LastName = "Łukasiewicz" };
        db.Drivers.AddRange(own, other, unassigned, otherTenant);
        db.PlanningDuties.AddRange(new PlanningDuty { Id = Guid.NewGuid(), CompanyId = tenant, OperatingCompanyId = operating, DutyNumber = "RN" },
            new PlanningDuty { Id = Guid.NewGuid(), CompanyId = tenant, DutyNumber = "RN" });
        var schedule = new PlanningSchedule { Id = Guid.NewGuid(), CompanyId = tenant, OperatingCompanyId = operating };
        db.PlanningSchedules.Add(schedule);
        db.PlanningAssignments.Add(new PlanningAssignment { Id = Guid.NewGuid(), CompanyId = tenant,
            DriverId = own.Id, Driver = own, PlanningScheduleId = schedule.Id, PlanningSchedule = schedule,
            Date = new(2026, 10, 31), Status = PlanningAssignmentStatus.Manual });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new LocalPlanningSyncPreviewService(db, new TestUser(tenant, operating));
        var preview = await service.PreviewAsync(Snapshot(), default);
        CollectionAssert.AreEqual(new[] { own.Id }, preview.DriverSuggestions[0].CandidateIds);
        Assert.AreEqual(1, preview.DutySuggestions[0].CandidateIds.Count);
        Assert.AreEqual(1, preview.PossibleManualConflicts);
        Assert.AreEqual(0, preview.PossibleGeneratedConflicts);
        Assert.AreEqual(4, await db.Drivers.CountAsync());
        Assert.AreEqual(1, await db.PlanningAssignments.CountAsync());
        Assert.AreEqual(0, db.ChangeTracker.Entries().Count());
    }

    [TestMethod]
    public async Task UnauthenticatedAndMobileUsersAreRejected()
    {
        using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => new LocalPlanningSyncPreviewService(db,
            new TestUser(Guid.Empty, null)).PreviewAsync(Snapshot(), default));
        await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => new LocalPlanningSyncPreviewService(db,
            new TestUser(Guid.NewGuid(), null, true)).PreviewAsync(Snapshot(), default));
    }

    private sealed class TestUser(Guid companyId, Guid? operatingCompanyId, bool mobile = false) : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid();
        public Guid CompanyId => companyId;
        public Guid? OperatingCompanyId => operatingCompanyId;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => mobile;
        public bool IsAuthenticated => companyId != Guid.Empty;
    }
}
