using DriverTime.Application.Downloads.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Downloads;

[TestClass]
public class CardReadDateTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void ParserUsesCardDataNotFileModificationTime()
    {
        var actual = CardReadDateResolver.FromParser("2026-10-27T21:45:06Z", "CardDownload", "card.ddd", Now);
        Assert.AreEqual("CardDownload", actual!.Source);
        Assert.AreEqual(new DateTime(2026, 10, 27, 21, 45, 6, DateTimeKind.Utc), actual.AtUtc);
        Assert.IsNull(CardReadDateResolver.FromParser("2026-10-28", "data modyfikacji pliku .DDD", "card.ddd", Now));
    }

    [TestMethod]
    public void FilenameDateUsesPolishTimeAndDoesNotChangeWhenImportedAgain()
    {
        var first = CardReadDateResolver.Resolve(null, null, "Szymczyk_Piotr_20261027234506.DDD", Now);
        var later = CardReadDateResolver.Resolve(null, null, "Szymczyk_Piotr_20261027234506.DDD", Now.AddDays(50));
        Assert.AreEqual(new DateTime(2026, 10, 27, 22, 45, 6, DateTimeKind.Utc), first!.AtUtc);
        Assert.AreEqual(first, later); Assert.AreEqual("OriginalFileName", first.Source);
    }

    [DataTestMethod]
    [DataRow("card.ddd")]
    [DataRow("card_20260230200000.DDD")]
    [DataRow("card_20301027200000.DDD")]
    [DataRow("card_20261025023000.DDD")]
    public void MissingInvalidFutureOrAmbiguousDateIsNotGuessed(string file)
        => Assert.IsNull(CardReadDateResolver.Resolve(null, null, file, Now));

    [TestMethod]
    public async Task ReadoutDateIsIndependentOfOldActivitiesAndRecentReupload()
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), CardNumber = "1234567890123400" };
        db.Drivers.Add(driver);
        var date = DateTime.UtcNow.AddDays(-1);
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = driver.CompanyId, DriverId = driver.Id,
            Driver = driver, DriverCardNumber = driver.CardNumber, CardReadAtUtc = date,
            CardReadDateSource = "CardDownload", UploadedAtUtc = DateTime.UtcNow };
        db.DddFiles.Add(file);
        db.DriverActivities.Add(new DriverActivity { Id = Guid.NewGuid(), DddFile = file, DddFileId = file.Id,
            ActivityType = "DRIVING", StartUtc = date.AddYears(-1).AddHours(-1), EndUtc = date.AddYears(-1) });
        var old = new DddFile { Id = Guid.NewGuid(), CompanyId = driver.CompanyId, DriverId = driver.Id,
            Driver = driver, DriverCardNumber = driver.CardNumber, UploadedAtUtc = DateTime.UtcNow.AddHours(1),
            CardReadAtUtc = date.AddDays(-40), CardReadDateSource = "CardDownload" };
        db.DddFiles.Add(old); await db.SaveChangesAsync();
        var row = (await new DownloadScheduleService(db).GetDriverDownloadsAsync(driver.CompanyId, null)).Single();
        Assert.AreEqual(date, row.LastDownloadUtc); Assert.AreEqual(DownloadStatus.Ok, row.Status);
        Assert.AreEqual(date.AddDays(28), row.NextRequiredDownloadUtc);
        Assert.AreEqual("CardDownload", row.LastDownloadSource);
    }

    [TestMethod]
    public async Task HistoricalImportUsesOriginalFilenameWithoutReimportOrBackfill()
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), CardNumber = "1234567890123400" };
        var localRead = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddDays(-1),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw"));
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = driver.CompanyId, DriverId = driver.Id,
            Driver = driver, DriverCardNumber = driver.CardNumber,
            FileName = $"Szymczyk_Piotr_{localRead:yyyyMMddHHmmss}.DDD", UploadedAtUtc = DateTime.UtcNow };
        db.Drivers.Add(driver); db.DddFiles.Add(file); await db.SaveChangesAsync();
        var row = (await new DownloadScheduleService(db).GetDriverDownloadsAsync(driver.CompanyId, null)).Single();
        Assert.AreEqual(DownloadStatus.Ok, row.Status);
        Assert.AreEqual("OriginalFileName", row.LastDownloadSource);
        Assert.IsNull(file.CardReadAtUtc);
    }
}
