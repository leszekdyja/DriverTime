using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Reports;

[TestClass]
public class CompanyReportTests
{
    [TestMethod]
    public void GetUtcRange_UsesPolishWinterTime()
    {
        var range = DriverReportExportService.GetUtcRange(
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15));

        Assert.AreEqual(new DateTime(2026, 1, 14, 23, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.AreEqual(new DateTime(2026, 1, 15, 23, 0, 0, DateTimeKind.Utc), range.ToUtcExclusive);
    }

    [TestMethod]
    public void GetUtcRange_UsesPolishSummerTime()
    {
        var range = DriverReportExportService.GetUtcRange(
            new DateOnly(2026, 7, 15),
            new DateOnly(2026, 7, 15));

        Assert.AreEqual(new DateTime(2026, 7, 14, 22, 0, 0, DateTimeKind.Utc), range.FromUtc);
        Assert.AreEqual(new DateTime(2026, 7, 15, 22, 0, 0, DateTimeKind.Utc), range.ToUtcExclusive);
    }

    [TestMethod]
    public async Task Activities_FilterByOperatingCompany_ReturnsOnlyAssignedDrivers()
    {
        var tenantId = Guid.NewGuid();
        var selectedCompanyId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var selectedDriver = CreateDriver(tenantId, selectedCompanyId, "Jan");
        var otherDriver = CreateDriver(tenantId, Guid.NewGuid(), "Adam");
        db.Drivers.AddRange(selectedDriver, otherDriver);
        db.DriverActivities.AddRange(
            CreateActivity(tenantId, selectedDriver, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc)),
            CreateActivity(tenantId, otherDriver, new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var result = await new DriverActivityService(db, new TestCurrentUserService(tenantId))
            .GetActivitiesAsync(null, null, null, null, selectedCompanyId);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Jan", result[0].DriverFirstName);
    }

    private static Driver CreateDriver(Guid tenantId, Guid operatingCompanyId, string firstName) => new()
    {
        Id = Guid.NewGuid(), CompanyId = tenantId, OperatingCompanyId = operatingCompanyId,
        FirstName = firstName, LastName = "Testowy", CardNumber = Guid.NewGuid().ToString("N")
    };

    private static DriverActivity CreateActivity(Guid tenantId, Driver driver, DateTime start) => new()
    {
        Id = Guid.NewGuid(), StartUtc = start, EndUtc = start.AddHours(1), ActivityType = "DRIVING",
        DddFile = new DddFile
        {
            Id = Guid.NewGuid(), CompanyId = tenantId, Driver = driver, DriverId = driver.Id,
            FileName = "test.ddd", FileHash = Guid.NewGuid().ToString("N"),
            DriverFirstName = driver.FirstName, DriverLastName = driver.LastName, DriverCardNumber = driver.CardNumber
        }
    };

    private static DriverTimeDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestCurrentUserService(Guid companyId) : ICurrentUserService
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = companyId;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => true;
    }
}
