using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Drivers;

[TestClass]
public class DriverDetailsLoadingTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DetailsReturnLatestSavedViolationsWithOptionalOperatingCompany(bool scoped)
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid();
        var firm = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, OperatingCompanyId = firm };
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = company, DriverId = driver.Id, Driver = driver };
        var start = DateTime.UtcNow.Date.AddDays(-20);
        db.DriverActivities.Add(new DriverActivity
        {
            Id = Guid.NewGuid(), DddFile = file, DddFileId = file.Id,
            ActivityType = "DRIVING", StartUtc = start, EndUtc = start.AddHours(2),
            IsCrew = true, CardSlot = 1
        });
        db.Violations.AddRange(Enumerable.Range(1, 12).Concat(new[] { -40, 25 }).Select(day => new Violation
        {
            Id = Guid.NewGuid(), DriverId = driver.Id,
            RegulationReference = "DAILY_REST", Severity = "high",
            ViolationStart = start.AddDays(day), ViolationEnd = start.AddDays(day).AddHours(1)
        }));
        db.Violations.Add(new Violation
        {
            Id = Guid.NewGuid(), DriverId = Guid.NewGuid(),
            ViolationStart = start.AddDays(30), ViolationEnd = start.AddDays(30).AddHours(1)
        });
        await db.SaveChangesAsync();
        var user = new User(company, scoped ? firm : null);
        var service = new DriverService(db, user, NullLogger<DriverService>.Instance);

        var details = await service.GetByIdAsync(driver.Id);
        var violationQuery = new ViolationQueryService(db, NullLogger<ViolationQueryService>.Instance);
        var historicViolations = await violationQuery.GetAsync(company, user.OperatingCompanyId, driver.Id,
            start.AddDays(-41), start.AddDays(-39), null, null);
        Assert.AreEqual(0, historicViolations.Count);

        Assert.IsNotNull(details);
        Assert.AreEqual(driver.Id, details.Id);
        Assert.AreEqual(1, details.ImportsCount);
        Assert.AreEqual(7200L, details.DrivingSeconds);
        Assert.AreEqual(true, details.RecentActivities.Single().IsCrew);
        Assert.AreEqual(1, details.RecentActivities.Single().CardSlot);
        CollectionAssert.AreEqual(
            Enumerable.Range(3, 10).Reverse().Select(day => start.AddDays(day)).ToArray(),
            details.RecentViolations.Select(x => x.OccurredAtUtc).ToArray());
    }

    private sealed class User(Guid company, Guid? scope) : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public Guid CompanyId => company;
        public Guid? OperatingCompanyId => scope;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => true;
    }
}

