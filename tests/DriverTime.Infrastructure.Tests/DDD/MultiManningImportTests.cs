using System.Text.Json;
using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.DDD;

[TestClass]
public class MultiManningImportTests
{
    private const string Json = """
        [{"start":"2026-10-01T00:00:00Z","end":"2026-10-01T04:30:00Z",
          "activity_code":"DRIVING","is_crew":true,"card_slot":1},
         {"start":"2026-10-01T04:30:00Z","end":"2026-10-01T09:00:00Z",
          "activity_code":"AVAILABILITY","is_crew":true,"card_slot":2}]
        """;

    [TestMethod]
    public async Task ParserContractSurvivesDatabaseTimelineAndActivityReport()
    {
        var parsed = JsonSerializer.Deserialize<List<ParsedDriverActivityDto>>(Json)!;
        var company = Guid.NewGuid();
        var driver = new Driver { Id = Guid.NewGuid(), CompanyId = company, CardNumber = "CREW_TEST" };
        var file = new DddFile { Id = Guid.NewGuid(), CompanyId = company, Driver = driver,
            DriverId = driver.Id, DriverCardNumber = driver.CardNumber };
        DddFileService.AddActivities(file, parsed, new DateTime(2026, 11, 1));
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.DddFiles.Add(file);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var timeline = await new TimelineBuilderService(db, NullLogger<TimelineBuilderService>.Instance)
            .BuildForDriverAsync(company, driver.Id);
        Assert.IsNotNull(timeline);
        Assert.AreEqual(2, timeline.Count);
        Assert.IsTrue(timeline.All(x => x.IsCrew == true));
        Assert.AreEqual(2, timeline[1].CardSlot);
        var report = await new DriverActivityService(db, new User(company)).GetActivitiesAsync(null, null, null, driver.Id);
        Assert.IsTrue(report.All(x => x.IsCrew == true));
        Assert.AreEqual(2, report[1].CardSlot);
        Assert.IsTrue(db.Model.FindEntityType(typeof(DriverActivity))!.FindProperty(nameof(DriverActivity.IsCrew))!.IsNullable);
    }

    [TestMethod]
    public void DuplicateDownloadEnrichesOnlyMatchingUnknownMetadataWithoutChangingActivities()
    {
        var parsed = JsonSerializer.Deserialize<List<ParsedDriverActivityDto>>(Json)!;
        var file = new DddFile();
        DddFileService.AddActivities(file, parsed, new DateTime(2026, 11, 1));
        foreach (var activity in file.DriverActivities) { activity.IsCrew = null; activity.CardSlot = null; }
        var ids = file.DriverActivities.Select(x => x.Id).ToArray();
        Assert.AreEqual(2, DddFileService.EnrichCrewMetadata(file, parsed));
        CollectionAssert.AreEqual(ids, file.DriverActivities.Select(x => x.Id).ToArray());
        Assert.AreEqual(0, DddFileService.EnrichCrewMetadata(file, parsed));
        Assert.AreEqual(2, file.DriverActivities.Count);
    }

    [TestMethod]
    public void DuplicateDownloadDoesNotGuessCrewWhenIntervalOrTypeDiffers()
    {
        var parsed = JsonSerializer.Deserialize<List<ParsedDriverActivityDto>>(Json)!;
        var file = new DddFile();
        DddFileService.AddActivities(file, parsed, new DateTime(2026, 11, 1));
        foreach (var activity in file.DriverActivities)
        {
            activity.IsCrew = null;
            activity.EndUtc = activity.EndUtc.AddMinutes(1);
        }
        Assert.AreEqual(0, DddFileService.EnrichCrewMetadata(file, parsed));
    }

    [TestMethod]
    public void DuplicateDownloadRejectsConflictingEvidence()
    {
        var parsed = JsonSerializer.Deserialize<List<ParsedDriverActivityDto>>(Json)!;
        var file = new DddFile();
        DddFileService.AddActivities(file, parsed.Take(1), new DateTime(2026, 11, 1));
        file.DriverActivities.Single().IsCrew = null;
        parsed.Add(new ParsedDriverActivityDto { Start = parsed[0].Start, End = parsed[0].End,
            ActivityCode = "DRIVING", IsCrew = false, CardSlot = 1 });
        Assert.AreEqual(0, DddFileService.EnrichCrewMetadata(file, parsed));
    }

    private sealed class User(Guid company) : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public Guid CompanyId => company;
        public Guid? OperatingCompanyId => null;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => true;
    }
}
