using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Reports;

[TestClass]
public class DriverReportVehicleMatchingTests
{
    [TestMethod]
    public void FindVehicleRegistration_ActivityOverlapsVehicleUse_ReturnsRegistration()
    {
        var dddFileId = Guid.NewGuid();
        var activity = Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z");
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DW 12345", "2026-05-06T07:00:00Z", "2026-05-06T12:00:00Z")
        };

        var registration = DriverReportExportService.FindVehicleRegistration(activity, vehicleUses);

        Assert.AreEqual("DW 12345", registration);
    }

    [TestMethod]
    public void FindVehicleRegistration_MultipleVehicleUsesInOneFile_ReturnsBestOverlap()
    {
        var dddFileId = Guid.NewGuid();
        var activity = Activity(dddFileId, "2026-05-06T12:00:00Z", "2026-05-06T14:00:00Z");
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DW 11111", "2026-05-06T08:00:00Z", "2026-05-06T12:30:00Z"),
            VehicleUse(dddFileId, "DW 22222", "2026-05-06T12:30:00Z", "2026-05-06T15:00:00Z")
        };

        var registration = DriverReportExportService.FindVehicleRegistration(activity, vehicleUses);

        Assert.AreEqual("DW 22222", registration);
    }

    [TestMethod]
    public void FindVehicleRegistration_ActivityWithoutVehicleInSameFile_ReturnsEmpty()
    {
        var activity = Activity(Guid.NewGuid(), "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z");
        var vehicleUses = new[]
        {
            VehicleUse(Guid.NewGuid(), "DW 12345", "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z")
        };

        var registration = DriverReportExportService.FindVehicleRegistration(activity, vehicleUses);

        Assert.AreEqual(string.Empty, registration);
    }

    [TestMethod]
    public void FindVehicleRegistration_ActivityTouchesVehicleUseBoundary_ReturnsNearestSameFileVehicle()
    {
        var dddFileId = Guid.NewGuid();
        var activity = Activity(dddFileId, "2026-05-06T10:00:00Z", "2026-05-06T11:00:00Z");
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DW 12345", "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z")
        };

        var registration = DriverReportExportService.FindVehicleRegistration(activity, vehicleUses);

        Assert.AreEqual("DW 12345", registration);
    }

    [TestMethod]
    public void BuildReportActivities_DuplicateActivityOverlappingMultipleVehicleUses_CountsActivityOnce()
    {
        var dddFileId = Guid.NewGuid();
        var otherDddFileId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", driverId, new Guid("00000000-0000-0000-0000-000000000001")),
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", driverId, new Guid("00000000-0000-0000-0000-000000000002")),
            Activity(otherDddFileId, "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", driverId, new Guid("00000000-0000-0000-0000-000000000003"))
        };
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DW 11111", "2026-05-06T07:30:00Z", "2026-05-06T08:30:00Z"),
            VehicleUse(dddFileId, "DW 22222", "2026-05-06T08:30:00Z", "2026-05-06T10:30:00Z"),
            VehicleUse(dddFileId, "DW 33333", "2026-05-06T09:00:00Z", "2026-05-06T11:00:00Z")
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(1, reportActivities.Count);
        Assert.AreEqual(7200, reportActivities[0].DurationSeconds);
        Assert.AreEqual("DW 22222", reportActivities[0].VehicleRegistration);
    }

    [TestMethod]
    public void BuildReportActivities_ActivityWithoutVehicle_ShowsMissingVehicleText()
    {
        var dddFileId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z")
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            Array.Empty<DriverReportExportService.VehicleUseReportSource>(),
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(1, reportActivities.Count);
        Assert.AreEqual("Brak danych", reportActivities[0].VehicleRegistration);
    }

    private static DriverReportExportService.DriverReportActivitySource Activity(
        Guid dddFileId,
        string startUtc,
        string endUtc,
        Guid? driverId = null,
        Guid? activityId = null)
    {
        return new DriverReportExportService.DriverReportActivitySource
        {
            Id = activityId ?? Guid.NewGuid(),
            DddFileId = dddFileId,
            DriverId = driverId,
            DriverCardNumber = driverId.HasValue ? string.Empty : "CARD-123",
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime(),
            ActivityType = "DRIVING"
        };
    }

    private static DriverReportExportService.VehicleUseReportSource VehicleUse(
        Guid dddFileId,
        string registrationNumber,
        string startUtc,
        string endUtc)
    {
        return new DriverReportExportService.VehicleUseReportSource
        {
            DddFileId = dddFileId,
            RegistrationNumber = registrationNumber,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }
}
