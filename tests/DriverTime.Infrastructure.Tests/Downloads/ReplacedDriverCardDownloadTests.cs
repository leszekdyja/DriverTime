using DriverTime.Application.Downloads.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Downloads;

[TestClass]
public class ReplacedDriverCardDownloadTests
{
    private static DriverTimeDbContext Database() => new(new DbContextOptionsBuilder<DriverTimeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Driver Card(Guid company, bool old, Guid? scope = null) => new() {
        Id = Guid.NewGuid(), CompanyId = company, OperatingCompanyId = scope,
        FirstName = "Jan", LastName = "Kowalski", CardIssuingCountry = "PL",
        CardNumber = old ? "1234567890123401" : "1234567890123402",
        CardExpiryDate = DateTime.UtcNow.Date.AddYears(old ? -1 : 4)
    };

    private static void Readout(DriverTimeDbContext db, Driver driver, int daysAgo, string? card = null)
    {
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = driver.CompanyId, DriverId = driver.Id,
            Driver = driver, DriverCardNumber = card ?? driver.CardNumber, UploadedAtUtc = DateTime.UtcNow };
        db.DddFiles.Add(file);
        db.DriverActivities.Add(new DriverActivity { Id = Guid.NewGuid(), DddFileId = file.Id, DddFile = file,
            ActivityType = "DRIVING", StartUtc = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-1),
            EndUtc = DateTime.UtcNow.AddDays(-daysAgo) });
    }

    [TestMethod]
    public async Task ConfirmedReplacementSuppressesOnlyExpiredCardAndPreservesHistory()
    {
        await using var db = Database();
        var company = Guid.NewGuid(); var old = Card(company, true); var current = Card(company, false);
        db.Drivers.AddRange(old, current); Readout(db, old, 90); Readout(db, current, 1);
        await db.SaveChangesAsync();
        var service = new DownloadScheduleService(db);
        var rows = await service.GetDriverDownloadsAsync(company, null);
        Assert.AreEqual(1, rows.Count); Assert.AreEqual(current.Id, rows[0].DriverId);
        Assert.AreEqual(DownloadStatus.Ok, rows[0].Status);
        Assert.AreEqual(0, (await service.GetDashboardAsync(company, null)).OverdueDrivers);
        Assert.AreEqual(2, await db.Drivers.CountAsync()); Assert.AreEqual(2, await db.DddFiles.CountAsync());
    }

    [TestMethod]
    public async Task OverdueCurrentCardIsNotHidden()
    {
        await using var db = Database();
        var company = Guid.NewGuid(); var old = Card(company, true); var current = Card(company, false);
        db.Drivers.AddRange(old, current); Readout(db, old, 90); Readout(db, current, 40);
        await db.SaveChangesAsync();
        var rows = await new DownloadScheduleService(db).GetDriverDownloadsAsync(company, null);
        Assert.AreEqual(1, rows.Count); Assert.AreEqual(current.Id, rows[0].DriverId);
        Assert.AreEqual(DownloadStatus.Overdue, rows[0].Status);
    }

    [TestMethod]
    public async Task UnconfirmedReplacementDoesNotSilenceOldCard()
    {
        await using var db = Database();
        var company = Guid.NewGuid(); var old = Card(company, true); var current = Card(company, false);
        db.Drivers.AddRange(old, current); Readout(db, old, 90); await db.SaveChangesAsync();
        var rows = await new DownloadScheduleService(db).GetDriverDownloadsAsync(company, null);
        Assert.IsTrue(rows.Any(x => x.DriverId == old.Id && x.Status == DownloadStatus.Overdue));
    }

    [TestMethod]
    public async Task AnotherCompanyOrOperatingCompanyCannotSuppressVisibleCard()
    {
        await using var db = Database();
        var company = Guid.NewGuid(); var scope = Guid.NewGuid();
        var old = Card(company, true, scope);
        var otherScope = Card(company, false, Guid.NewGuid()); var otherTenant = Card(Guid.NewGuid(), false, scope);
        db.Drivers.AddRange(old, otherScope, otherTenant);
        Readout(db, old, 90); Readout(db, otherScope, 1); Readout(db, otherTenant, 1); await db.SaveChangesAsync();
        var rows = await new DownloadScheduleService(db).GetDriverDownloadsAsync(company, scope);
        Assert.AreEqual(1, rows.Count); Assert.AreEqual(old.Id, rows[0].DriverId);
        Assert.AreEqual(DownloadStatus.Overdue, rows[0].Status);
    }

    [TestMethod]
    public void SameNameAloneOrAnotherPersonDoesNotEstablishReplacement()
    {
        var company = Guid.NewGuid(); var old = Card(company, true); var current = Card(company, false);
        current.CardNumber = "9999999999999902";
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
        current.CardNumber = "1234567890123402"; current.FirstName = "Adam";
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
        current.FirstName = "Jan"; current.CardExpiryDate = DateTime.UtcNow.AddDays(-1);
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
    }

    [TestMethod]
    public async Task UploadOfHistoricalCardDoesNotRefreshCurrentCardDeadline()
    {
        await using var db = Database();
        var current = Card(Guid.NewGuid(), false); db.Drivers.Add(current);
        Readout(db, current, 40); Readout(db, current, 1, "1234567890123401"); await db.SaveChangesAsync();
        var row = (await new DownloadScheduleService(db).GetDriverDownloadsAsync(current.CompanyId, null)).Single();
        Assert.AreEqual(DownloadStatus.Overdue, row.Status);
        Assert.IsTrue(row.LastDownloadUtc < DateTime.UtcNow.AddDays(-39));
    }

    [TestMethod]
    public async Task ConfirmedSameExpiryReplacementSuppressesObsoleteCardNotHistory()
    {
        await using var db = Database();
        var company = Guid.NewGuid(); var old = Card(company, false); var current = Card(company, false);
        old.CardNumber = "1691022063120003"; current.CardNumber = "1691022063120013";
        old.CardExpiryDate = current.CardExpiryDate;
        db.Drivers.AddRange(old, current); Readout(db, old, 90); Readout(db, current, 12);
        await db.SaveChangesAsync();
        var service = new DownloadScheduleService(db);
        var row = (await service.GetDriverDownloadsAsync(company, null)).Single();
        Assert.AreEqual(current.Id, row.DriverId); Assert.AreEqual(DownloadStatus.Ok, row.Status);
        Assert.AreEqual(0, (await service.GetDashboardAsync(company, null)).OverdueDrivers);
        Assert.AreEqual(2, await db.Drivers.CountAsync()); Assert.AreEqual(2, await db.DddFiles.CountAsync());
    }

    [TestMethod]
    public async Task SameExpiryReplacementStillRequiresImportedEvidenceAndKeepsRealOverdueAlert()
    {
        await using var db = Database();
        var company = Guid.NewGuid(); var old = Card(company, false); var current = Card(company, false);
        old.CardNumber = "1691022063120003"; current.CardNumber = "1691022063120013";
        old.CardExpiryDate = current.CardExpiryDate;
        db.Drivers.AddRange(old, current); Readout(db, old, 90); await db.SaveChangesAsync();
        var service = new DownloadScheduleService(db);
        Assert.AreEqual(2, (await service.GetDriverDownloadsAsync(company, null)).Count);
        Readout(db, current, 40); await db.SaveChangesAsync();
        var row = (await service.GetDriverDownloadsAsync(company, null)).Single();
        Assert.AreEqual(current.Id, row.DriverId); Assert.AreEqual(DownloadStatus.Overdue, row.Status);
    }

    [TestMethod]
    public void SameExpiryReplacementRequiresSamePersonScopeAndForwardReplacementIndex()
    {
        var company = Guid.NewGuid(); var old = Card(company, false); var current = Card(company, false);
        old.CardNumber = "1691022063120003"; current.CardNumber = "1691022063120013";
        old.CardExpiryDate = current.CardExpiryDate;
        Assert.IsTrue(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(current, old, DateTime.UtcNow));
        current.CardNumber = "1691022063120014";
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
        current.CardNumber = "1691022063120013"; current.FirstName = "Adam";
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
        current.FirstName = old.FirstName; current.OperatingCompanyId = Guid.NewGuid();
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
        current.OperatingCompanyId = null; current.CompanyId = Guid.NewGuid();
        Assert.IsFalse(DownloadScheduleService.IsSupersededDriverCard(old, current, DateTime.UtcNow));
    }
}
