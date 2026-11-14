using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Drivers;

[TestClass]
public class RestorableDriverDeletionTests
{
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ArchiveAndRestorePreserveHistoryAndOriginalStatus(bool active)
    {
        await using var db = Context();
        var company = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, IsActive = active };
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = company, Driver = driver, DriverId = driver.Id };
        db.DddFiles.Add(file);
        await db.SaveChangesAsync();
        var service = Service(db, company);
        Assert.IsTrue(await service.DeleteAsync(driver.Id));
        var deletedAt = driver.DeletedAtUtc;
        Assert.IsNotNull(deletedAt);
        Assert.IsFalse(driver.IsActive);
        Assert.AreEqual(0, await db.Drivers.CountAsync());
        Assert.AreEqual(1, await db.Drivers.IgnoreQueryFilters().CountAsync());
        Assert.AreEqual(1, await db.DddFiles.CountAsync());
        var archived = (await service.GetDeletedAsync()).Single();
        Assert.AreEqual(deletedAt.Value.AddMonths(1), archived.RestoreUntilUtc);
        Assert.IsTrue(await service.DeleteAsync(driver.Id));
        Assert.AreEqual(deletedAt, driver.DeletedAtUtc);
        Assert.IsTrue(await service.RestoreAsync(driver.Id));
        Assert.AreEqual(active, driver.IsActive);
        Assert.IsNull(driver.DeletedAtUtc);
        Assert.AreEqual(file.DriverId, driver.Id);
    }

    [TestMethod]
    public async Task ExpiredRestoreIsDeniedWithoutPurgingData()
    {
        await using var db = Context();
        var company = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, DeletedAtUtc = DateTime.UtcNow.AddMonths(-2), IsActive = false };
        db.Drivers.Add(driver); await db.SaveChangesAsync();
        var service = Service(db, company);
        Assert.IsFalse((await service.GetDeletedAsync()).Single().CanRestore);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => service.RestoreAsync(driver.Id));
        Assert.AreEqual(1, await db.Drivers.IgnoreQueryFilters().CountAsync());
    }

    [TestMethod]
    public async Task AnotherTenantCannotDeleteListOrRestoreArchivedDriver()
    {
        await using var db = Context();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid() };
        db.Drivers.Add(driver); await db.SaveChangesAsync();
        var foreign = Service(db, Guid.NewGuid());
        Assert.IsFalse(await foreign.DeleteAsync(driver.Id));
        await Service(db, driver.CompanyId).DeleteAsync(driver.Id);
        Assert.AreEqual(0, (await foreign.GetDeletedAsync()).Count);
        Assert.IsFalse(await foreign.RestoreAsync(driver.Id));
    }

    [TestMethod]
    public async Task HistoricalScheduleStillContainsArchivedDriverAndAssignments()
    {
        await using var db = Context();
        var company = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, LastName = "Czajka" };
        var schedule = new PlanningSchedule { Id = Guid.NewGuid(), CompanyId = company, Year = 2026, Month = 11 };
        var assignment = new PlanningAssignment { Id = Guid.NewGuid(), CompanyId = company, Driver = driver, DriverId = driver.Id,
            PlanningSchedule = schedule, PlanningScheduleId = schedule.Id, Date = new DateOnly(2026, 11, 1) };
        db.PlanningAssignments.Add(assignment); await db.SaveChangesAsync();
        await Service(db, company).DeleteAsync(driver.Id);
        var history = await new PlanningScheduleService(db, new User(company)).GetScheduleAsync(schedule.Id);
        Assert.IsNotNull(history);
        Assert.AreEqual(assignment.Id, history.Assignments.Single().Id);
        Assert.AreEqual(1, await db.PlanningAssignments.IgnoreQueryFilters().CountAsync());
        Assert.AreEqual(0, (await Service(db, company).GetAllAsync()).Count);
    }

    [TestMethod]
    public async Task SameTenantDifferentFirmCannotArchiveListOrRestore()
    {
        await using var db = Context();
        var company = Guid.NewGuid(); var firstScope = Guid.NewGuid(); var otherScope = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, OperatingCompanyId = firstScope };
        db.Drivers.Add(driver); await db.SaveChangesAsync();
        var foreign = new DriverService(db, new User(company, otherScope), NullLogger<DriverService>.Instance);
        Assert.IsFalse(await foreign.DeleteAsync(driver.Id));
        await Service(db, company).DeleteAsync(driver.Id);
        Assert.AreEqual(0, (await foreign.GetDeletedAsync()).Count);
        Assert.IsFalse(await foreign.RestoreAsync(driver.Id));
    }

    private static DriverTimeDbContext Context() => new(new DbContextOptionsBuilder<DriverTimeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static DriverService Service(DriverTimeDbContext db, Guid company) => new(db, new User(company), NullLogger<DriverService>.Instance);
    private sealed class User(Guid company, Guid? scope = null) : ICurrentUserService
    {
        public Guid UserId => Guid.NewGuid(); public Guid CompanyId => company; public Guid DriverId => Guid.Empty;
        public Guid? OperatingCompanyId => scope;
        public bool IsMobileDriver => false; public bool IsAuthenticated => true;
    }
}
