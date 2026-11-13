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
    public async Task DetailsAndViolationsLoadWithOptionalOperatingCompany(bool scoped)
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company = Guid.NewGuid();
        var firm = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, OperatingCompanyId = firm };
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = company, DriverId = driver.Id, Driver = driver };
        var start = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        db.DriverActivities.Add(new DriverActivity
        {
            Id = Guid.NewGuid(), DddFile = file, DddFileId = file.Id,
            ActivityType = "DRIVING", StartUtc = start, EndUtc = start.AddHours(2),
            IsCrew = true, CardSlot = 1
        });
        await db.SaveChangesAsync();
        var user = new User(company, scoped ? firm : null);
        var violations = new DriverViolationService(db, user);
        var service = new DriverService(db, user, violations, NullLogger<DriverService>.Instance);

        var details = await service.GetByIdAsync(driver.Id);

        Assert.IsNotNull(details);
        Assert.AreEqual(driver.Id, details.Id);
        Assert.AreEqual(1, details.ImportsCount);
        Assert.AreEqual(7200L, details.DrivingSeconds);
        Assert.AreEqual(true, details.RecentActivities.Single().IsCrew);
        Assert.AreEqual(1, details.RecentActivities.Single().CardSlot);
        Assert.AreEqual(0, details.RecentViolations.Count);
        Assert.IsNotNull(await violations.GetViolationsForDriverAsync(driver.Id));
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

