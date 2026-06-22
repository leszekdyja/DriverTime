using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Reports;

[TestClass]
public class DriverMileageReportServiceTests
{
    [TestMethod]
    public void BuildReport_SumsOnlyRowsWithDistance()
    {
        var driverId = Guid.NewGuid();
        var report = DriverMileageReportService.BuildReport(
            driverId,
            "Kowalski Jan",
            DateOnly.FromDateTime(DateTime.Parse("2026-05-01")),
            DateOnly.FromDateTime(DateTime.Parse("2026-05-31")),
            new[]
            {
                Row("2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", "DW 12345", 1000, 1120, 120),
                Row("2026-05-07T08:00:00Z", "2026-05-07T10:00:00Z", "DW 12345", null, null, null),
                Row("2026-05-08T08:00:00Z", "2026-05-08T10:00:00Z", "DW 12345", 1120, 1195, 75)
            });

        Assert.AreEqual(driverId, report.DriverId);
        Assert.AreEqual("Kowalski Jan", report.DriverName);
        Assert.AreEqual(195, report.TotalDistanceKm);
        Assert.AreEqual(3, report.VehicleUseCount);
        Assert.AreEqual(1, report.MissingDistanceCount);
    }

    [TestMethod]
    public void BuildReport_NullDistanceIsNotReportedAsZero()
    {
        var report = DriverMileageReportService.BuildReport(
            Guid.NewGuid(),
            "Kowalski Jan",
            DateOnly.FromDateTime(DateTime.Parse("2026-05-01")),
            DateOnly.FromDateTime(DateTime.Parse("2026-05-31")),
            new[]
            {
                Row("2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", "DW 12345", null, null, null)
            });

        Assert.AreEqual(0, report.TotalDistanceKm);
        Assert.AreEqual(1, report.MissingDistanceCount);
        Assert.IsNull(report.Rows[0].DistanceKm);
        Assert.IsFalse(report.Rows[0].HasDistanceData);
    }

    [TestMethod]
    public void BuildReport_SortsRowsByStartUtc()
    {
        var report = DriverMileageReportService.BuildReport(
            Guid.NewGuid(),
            "Kowalski Jan",
            DateOnly.FromDateTime(DateTime.Parse("2026-05-01")),
            DateOnly.FromDateTime(DateTime.Parse("2026-05-31")),
            new[]
            {
                Row("2026-05-08T08:00:00Z", "2026-05-08T10:00:00Z", "DW 22222", 1100, 1200, 100),
                Row("2026-05-06T08:00:00Z", "2026-05-06T10:00:00Z", "DW 11111", 1000, 1100, 100)
            });

        Assert.AreEqual("DW 11111", report.Rows[0].RegistrationNumber);
        Assert.AreEqual("DW 22222", report.Rows[1].RegistrationNumber);
    }

    [TestMethod]
    public void BuildVehicleUseScope_FiltersByDateOverlap()
    {
        var driverId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-10T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-12T00:00:00Z").ToUniversalTime();
        var filter = DriverMileageReportService
            .BuildVehicleUseScope(driverId, companyId, fromUtc, toUtcExclusive)
            .Compile();

        Assert.IsTrue(filter(VehicleUse(driverId, companyId, "2026-05-09T23:00:00Z", "2026-05-10T01:00:00Z")));
        Assert.IsTrue(filter(VehicleUse(driverId, companyId, "2026-05-11T23:00:00Z", "2026-05-12T01:00:00Z")));
        Assert.IsFalse(filter(VehicleUse(driverId, companyId, "2026-05-09T08:00:00Z", "2026-05-09T10:00:00Z")));
        Assert.IsFalse(filter(VehicleUse(driverId, companyId, "2026-05-12T00:00:00Z", "2026-05-12T01:00:00Z")));
    }

    [TestMethod]
    public void BuildVehicleUseScope_FiltersByDriverAndCompany()
    {
        var driverId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var fromUtc = DateTime.Parse("2026-05-10T00:00:00Z").ToUniversalTime();
        var toUtcExclusive = DateTime.Parse("2026-05-12T00:00:00Z").ToUniversalTime();
        var filter = DriverMileageReportService
            .BuildVehicleUseScope(driverId, companyId, fromUtc, toUtcExclusive)
            .Compile();

        Assert.IsTrue(filter(VehicleUse(driverId, companyId, "2026-05-10T08:00:00Z", "2026-05-10T10:00:00Z")));
        Assert.IsFalse(filter(VehicleUse(Guid.NewGuid(), companyId, "2026-05-10T08:00:00Z", "2026-05-10T10:00:00Z")));
        Assert.IsFalse(filter(VehicleUse(driverId, Guid.NewGuid(), "2026-05-10T08:00:00Z", "2026-05-10T10:00:00Z")));
    }

    private static DriverMileageReportService.DriverMileageReportRowSource Row(
        string startUtc,
        string endUtc,
        string registrationNumber,
        int? startOdometerKm,
        int? endOdometerKm,
        int? distanceKm)
    {
        return new DriverMileageReportService.DriverMileageReportRowSource
        {
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime(),
            RegistrationNumber = registrationNumber,
            StartOdometerKm = startOdometerKm,
            EndOdometerKm = endOdometerKm,
            DistanceKm = distanceKm
        };
    }

    private static VehicleUse VehicleUse(
        Guid driverId,
        Guid companyId,
        string startUtc,
        string endUtc)
    {
        return new VehicleUse
        {
            DddFile = new DddFile
            {
                DriverId = driverId,
                CompanyId = companyId
            },
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }
}
