using DriverTime.Application.DDD.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

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

    [TestMethod]
    public void ParsedVehicleUseDto_JsonWithOdometerFields_DeserializesValues()
    {
        const string json = """
            {
              "start": "2026-05-17 05:02:00",
              "end": "2026-05-17 12:06:35",
              "vehicle_registration": "DLU 68178",
              "source": "embedded:EF0505@18277:record@3102",
              "start_odometer_km": 581224,
              "end_odometer_km": 581387,
              "distance_km": 163
            }
            """;

        var vehicleUse = JsonSerializer.Deserialize<ParsedVehicleUseDto>(json);

        Assert.IsNotNull(vehicleUse);
        Assert.AreEqual(581224, vehicleUse.StartOdometerKm);
        Assert.AreEqual(581387, vehicleUse.EndOdometerKm);
        Assert.AreEqual(163, vehicleUse.DistanceKm);
    }

    [TestMethod]
    public void ParsedVehicleUseDto_OldJsonWithoutOdometerFields_DeserializesNullValues()
    {
        const string json = """
            {
              "start": "2026-05-17 05:02:00",
              "end": "2026-05-17 12:06:35",
              "vehicle_registration": "DLU 68178",
              "source": "embedded:EF0505@18277:record@3102"
            }
            """;

        var vehicleUse = JsonSerializer.Deserialize<ParsedVehicleUseDto>(json);

        Assert.IsNotNull(vehicleUse);
        Assert.IsNull(vehicleUse.StartOdometerKm);
        Assert.IsNull(vehicleUse.EndOdometerKm);
        Assert.IsNull(vehicleUse.DistanceKm);
    }

    [TestMethod]
    public void AddVehicleUses_ValidOdometerValues_AddsRecordWithDistance()
    {
        var dddFile = new DddFile();
        var vehicleUse = new ParsedVehicleUseDto
        {
            Start = "2026-05-17T05:02:00Z",
            End = "2026-05-17T12:06:35Z",
            VehicleRegistration = "DLU 68178",
            StartOdometerKm = 581224,
            EndOdometerKm = 581387,
            DistanceKm = 163
        };

        DddFileService.AddVehicleUses(dddFile, new[] { vehicleUse }, NowUtc);

        Assert.AreEqual(1, dddFile.VehicleUses.Count);
        var savedVehicleUse = dddFile.VehicleUses.Single();
        Assert.AreEqual(581224, savedVehicleUse.StartOdometerKm);
        Assert.AreEqual(581387, savedVehicleUse.EndOdometerKm);
        Assert.AreEqual(163, savedVehicleUse.DistanceKm);
    }

    [TestMethod]
    public void AddVehicleUses_WithoutOdometerValues_AddsRecordWithNullOdometer()
    {
        var dddFile = new DddFile();
        var vehicleUse = new ParsedVehicleUseDto
        {
            Start = "2026-05-17T05:02:00Z",
            End = "2026-05-17T12:06:35Z",
            VehicleRegistration = "DLU 68178"
        };

        DddFileService.AddVehicleUses(dddFile, new[] { vehicleUse }, NowUtc);

        Assert.AreEqual(1, dddFile.VehicleUses.Count);
        var savedVehicleUse = dddFile.VehicleUses.Single();
        Assert.IsNull(savedVehicleUse.StartOdometerKm);
        Assert.IsNull(savedVehicleUse.EndOdometerKm);
        Assert.IsNull(savedVehicleUse.DistanceKm);
    }

    [TestMethod]
    public void IsDuplicateDddFileHashConstraint_WithFileHashIndex_ReturnsTrue()
    {
        Assert.IsTrue(DddFileService.IsDuplicateDddFileHashConstraint("IX_DddFiles_CompanyId_FileHash"));
        Assert.IsFalse(DddFileService.IsDuplicateDddFileHashConstraint("IX_Drivers_CompanyId_CardNumber"));
    }

    [TestMethod]
    public void IsDuplicateDriverCardNumberConstraint_WithDriverCardIndex_ReturnsTrue()
    {
        Assert.IsTrue(DddFileService.IsDuplicateDriverCardNumberConstraint("IX_Drivers_CompanyId_CardNumber"));
        Assert.IsFalse(DddFileService.IsDuplicateDriverCardNumberConstraint("IX_DddFiles_CompanyId_FileHash"));
    }

    [TestMethod]
    public void ApplyExistingDriverAfterConcurrentInsert_ReusesExistingDriverForImport()
    {
        var existingDriver = new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            CardNumber = "CARD-123",
            FirstName = "Jan",
            LastName = "Kowalski"
        };
        var dddFile = new DddFile
        {
            Id = Guid.NewGuid(),
            DriverId = Guid.NewGuid(),
            DriverFirstName = "",
            DriverLastName = "",
            DriverCreatedDuringImport = true
        };
        var parsedDriver = new ParsedDriverDto
        {
            FirstName = "Jan",
            LastName = "Kowalski",
            CardNumber = "CARD-123"
        };

        DddFileService.ApplyExistingDriverAfterConcurrentInsert(
            dddFile,
            existingDriver,
            parsedDriver);

        Assert.AreEqual(existingDriver.Id, dddFile.DriverId);
        Assert.AreEqual("Jan", dddFile.DriverFirstName);
        Assert.AreEqual("Kowalski", dddFile.DriverLastName);
        Assert.IsFalse(dddFile.DriverCreatedDuringImport);
    }

}
