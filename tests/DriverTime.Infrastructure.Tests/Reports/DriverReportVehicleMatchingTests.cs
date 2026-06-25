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
    public void FindVehicleRegistration_ActivityTouchesVehicleUseBoundary_ReturnsEmpty()
    {
        var dddFileId = Guid.NewGuid();
        var activity = Activity(dddFileId, "2026-05-06T10:00:00Z", "2026-05-06T11:00:00Z");
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DW 12345", "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z")
        };

        var registration = DriverReportExportService.FindVehicleRegistration(activity, vehicleUses);

        Assert.AreEqual(string.Empty, registration);
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
            VehicleUse(dddFileId, "DW 11111", "2026-05-06T07:30:00Z", "2026-05-06T08:30:00Z", 1000, 1020, 20),
            VehicleUse(dddFileId, "DW 22222", "2026-05-06T08:30:00Z", "2026-05-06T10:30:00Z", 1020, 1180, 160),
            VehicleUse(dddFileId, "DW 33333", "2026-05-06T09:00:00Z", "2026-05-06T11:00:00Z", 1180, 1250, 70)
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(1, reportActivities.Count);
        Assert.AreEqual(7200, reportActivities[0].DurationSeconds);
        Assert.AreEqual("DW 22222", reportActivities[0].VehicleRegistration);
        Assert.AreEqual(1020, reportActivities[0].StartOdometerKm);
        Assert.AreEqual(1180, reportActivities[0].EndOdometerKm);
        Assert.AreEqual(160, reportActivities[0].DistanceKm);
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

    [TestMethod]
    public void BuildReportActivities_MultipleActivitiesInSameVehicleUse_ShowsOdometerOnlyOnce()
    {
        var dddFileId = Guid.NewGuid();
        var vehicleUseId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T09:00:00Z"),
            Activity(dddFileId, "2026-05-06T09:00:00Z", "2026-05-06T10:00:00Z")
        };
        var vehicleUses = new[]
        {
            VehicleUse(
                dddFileId,
                "DW 12345",
                "2026-05-06T07:30:00Z",
                "2026-05-06T10:30:00Z",
                1000,
                1120,
                120,
                vehicleUseId)
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(2, reportActivities.Count);
        Assert.AreEqual(1000, reportActivities[0].StartOdometerKm);
        Assert.AreEqual(1120, reportActivities[0].EndOdometerKm);
        Assert.AreEqual(120, reportActivities[0].DistanceKm);
        Assert.IsNull(reportActivities[1].StartOdometerKm);
        Assert.IsNull(reportActivities[1].EndOdometerKm);
        Assert.IsNull(reportActivities[1].DistanceKm);
        Assert.AreEqual(120, DriverReportExportService.SumDistance(reportActivities));
    }

    [TestMethod]
    public void SumDistance_MultipleActivitiesAcrossTwoVehicleUses_CountsEachVehicleUseOnce()
    {
        var dddFileId = Guid.NewGuid();
        var firstVehicleUseId = Guid.NewGuid();
        var secondVehicleUseId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T09:00:00Z"),
            Activity(dddFileId, "2026-05-06T09:00:00Z", "2026-05-06T10:00:00Z"),
            Activity(dddFileId, "2026-05-06T10:00:00Z", "2026-05-06T11:00:00Z"),
            Activity(dddFileId, "2026-05-06T11:00:00Z", "2026-05-06T12:00:00Z")
        };
        var vehicleUses = new[]
        {
            VehicleUse(
                dddFileId,
                "DW 11111",
                "2026-05-06T07:30:00Z",
                "2026-05-06T10:00:00Z",
                434727,
                435083,
                356,
                firstVehicleUseId),
            VehicleUse(
                dddFileId,
                "DW 22222",
                "2026-05-06T10:00:00Z",
                "2026-05-06T12:30:00Z",
                435083,
                435471,
                388,
                secondVehicleUseId)
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(4, reportActivities.Count);
        Assert.AreEqual(356, reportActivities[0].DistanceKm);
        Assert.IsNull(reportActivities[1].DistanceKm);
        Assert.AreEqual(388, reportActivities[2].DistanceKm);
        Assert.IsNull(reportActivities[3].DistanceKm);
        Assert.AreEqual(744, DriverReportExportService.SumDistance(reportActivities));
    }


    [TestMethod]
    public void BuildReportActivities_DuplicateVehicleUseWithDifferentIds_CountsDistanceOnce()
    {
        var dddFileId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T09:00:00Z"),
            Activity(dddFileId, "2026-05-06T09:00:00Z", "2026-05-06T10:00:00Z"),
            Activity(dddFileId, "2026-05-06T10:00:00Z", "2026-05-06T11:00:00Z"),
            Activity(dddFileId, "2026-05-06T11:00:00Z", "2026-05-06T12:00:00Z")
        };
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DPL 07532", "2026-05-06T08:00:00Z", "2026-05-06T12:00:00Z", 761093, 761137, 44, Guid.NewGuid()),
            VehicleUse(dddFileId, "DPL 07532", "2026-05-06T08:00:00Z", "2026-05-06T12:00:00Z", 761093, 761137, 44, Guid.NewGuid())
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(4, reportActivities.Count);
        Assert.AreEqual("DPL 07532", reportActivities[0].VehicleRegistration);
        Assert.AreEqual("DPL 07532", reportActivities[1].VehicleRegistration);
        Assert.AreEqual(761093, reportActivities[0].StartOdometerKm);
        Assert.AreEqual(761137, reportActivities[0].EndOdometerKm);
        Assert.AreEqual(44, reportActivities[0].DistanceKm);
        Assert.IsNull(reportActivities[1].StartOdometerKm);
        Assert.IsNull(reportActivities[2].DistanceKm);
        Assert.IsNull(reportActivities[3].DistanceKm);
        Assert.AreEqual(44, DriverReportExportService.SumDistance(reportActivities));
    }

    [TestMethod]
    public void SumDistance_TwoBusinessVehicleUses_AddsUniqueDistances()
    {
        var dddFileId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T09:00:00Z"),
            Activity(dddFileId, "2026-05-06T09:00:00Z", "2026-05-06T10:00:00Z"),
            Activity(dddFileId, "2026-05-06T12:00:00Z", "2026-05-06T13:00:00Z"),
            Activity(dddFileId, "2026-05-06T13:00:00Z", "2026-05-06T14:00:00Z")
        };
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DPL 07532", "2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", 761093, 761137, 44),
            VehicleUse(dddFileId, "DLU 68178", "2026-05-06T12:00:00Z", "2026-05-06T14:00:00Z", 581856, 582015, 159)
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(44, reportActivities[0].DistanceKm);
        Assert.IsNull(reportActivities[1].DistanceKm);
        Assert.AreEqual(159, reportActivities[2].DistanceKm);
        Assert.IsNull(reportActivities[3].DistanceKm);
        Assert.AreEqual(203, DriverReportExportService.SumDistance(reportActivities));
    }

    [TestMethod]
    public void BuildReportActivities_RestOverlappingVehicleUse_DoesNotShowDistance()
    {
        var dddFileId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-06T08:00:00Z", "2026-05-06T12:00:00Z", activityType: "REST"),
            Activity(dddFileId, "2026-05-06T12:00:00Z", "2026-05-06T13:00:00Z")
        };
        var vehicleUses = new[]
        {
            VehicleUse(dddFileId, "DPL 07532", "2026-05-06T08:00:00Z", "2026-05-06T13:00:00Z", 761093, 761137, 44)
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            vehicleUses,
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual("DPL 07532", reportActivities[0].VehicleRegistration);
        Assert.IsNull(reportActivities[0].StartOdometerKm);
        Assert.IsNull(reportActivities[0].DistanceKm);
        Assert.AreEqual(44, reportActivities[1].DistanceKm);
        Assert.AreEqual(44, DriverReportExportService.SumDistance(reportActivities));
    }

    [TestMethod]
    public void BuildReportActivities_ActivityClippedToReportRange_UsesVisibleTimesAndDuration()
    {
        var dddFileId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-06T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-07T00:00:00Z").ToUniversalTime();
        var activities = new[]
        {
            Activity(dddFileId, "2026-05-05T16:00:00Z", "2026-05-06T02:57:45Z", activityType: "REST")
        };

        var reportActivities = DriverReportExportService.BuildReportActivities(
            activities,
            Array.Empty<DriverReportExportService.VehicleUseReportSource>(),
            fromUtc,
            toUtcExclusive);

        Assert.AreEqual(fromUtc, reportActivities[0].StartUtc);
        Assert.AreEqual(DateTime.Parse("2026-05-06T02:57:45Z").ToUniversalTime(), reportActivities[0].EndUtc);
        Assert.AreEqual(10665, reportActivities[0].DurationSeconds);
    }
    private static DriverReportExportService.DriverReportActivitySource Activity(
        Guid dddFileId,
        string startUtc,
        string endUtc,
        Guid? driverId = null,
        Guid? activityId = null,
        string activityType = "DRIVING")
    {
        return new DriverReportExportService.DriverReportActivitySource
        {
            Id = activityId ?? Guid.NewGuid(),
            DddFileId = dddFileId,
            DriverId = driverId,
            DriverCardNumber = driverId.HasValue ? string.Empty : "CARD-123",
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime(),
            ActivityType = activityType
        };
    }

    private static DriverReportExportService.VehicleUseReportSource VehicleUse(
        Guid dddFileId,
        string registrationNumber,
        string startUtc,
        string endUtc,
        int? startOdometerKm = null,
        int? endOdometerKm = null,
        int? distanceKm = null,
        Guid? vehicleUseId = null)
    {
        return new DriverReportExportService.VehicleUseReportSource
        {
            Id = vehicleUseId ?? Guid.NewGuid(),
            DddFileId = dddFileId,
            RegistrationNumber = registrationNumber,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime(),
            StartOdometerKm = startOdometerKm,
            EndOdometerKm = endOdometerKm,
            DistanceKm = distanceKm
        };
    }
}
