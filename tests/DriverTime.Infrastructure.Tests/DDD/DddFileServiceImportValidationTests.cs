using DriverTime.Application.DDD.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.DDD;

[TestClass]
public class DddFileServiceImportValidationTests
{
    private static readonly DateTime NowUtc = new(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void AddActivities_ValidActivity_AddsRecord()
    {
        var dddFile = new DddFile();
        var activity = new ParsedDriverActivityDto
        {
            Start = "2026-04-29T08:00:00Z",
            End = "2026-04-29T09:00:00Z",
            Activity = "work",
            ActivityCode = "work"
        };

        DddFileService.AddActivities(dddFile, new[] { activity }, NowUtc);

        Assert.AreEqual(1, dddFile.DriverActivities.Count);
    }

    [TestMethod]
    public void AddActivities_ActivityFrom1991_SkipsRecord()
    {
        var dddFile = new DddFile();
        var activity = new ParsedDriverActivityDto
        {
            Start = "1991-11-09T08:00:00Z",
            End = "1991-11-09T09:00:00Z",
            Activity = "work",
            ActivityCode = "work"
        };

        DddFileService.AddActivities(dddFile, new[] { activity }, NowUtc);

        Assert.AreEqual(0, dddFile.DriverActivities.Count);
    }

    [TestMethod]
    public void AddActivities_ActivityFrom2038_SkipsRecord()
    {
        var dddFile = new DddFile();
        var activity = new ParsedDriverActivityDto
        {
            Start = "2038-04-21T08:00:00Z",
            End = "2038-04-21T09:00:00Z",
            Activity = "work",
            ActivityCode = "work"
        };

        DddFileService.AddActivities(dddFile, new[] { activity }, NowUtc);

        Assert.AreEqual(0, dddFile.DriverActivities.Count);
    }

    [TestMethod]
    public void AddVehicleUses_InvalidDate_SkipsRecord()
    {
        var dddFile = new DddFile();
        var vehicleUse = new ParsedVehicleUseDto
        {
            Start = "1991-11-09T08:00:00Z",
            End = "1991-11-09T09:00:00Z",
            VehicleRegistration = "DW12345"
        };

        DddFileService.AddVehicleUses(dddFile, new[] { vehicleUse }, NowUtc);

        Assert.AreEqual(0, dddFile.VehicleUses.Count);
    }
}
