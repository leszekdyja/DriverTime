using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleDetailsDateRangeTests
{
    [TestMethod]
    public void Create_WithoutParameters_KeepsExistingUnboundedBehavior()
    {
        var range = VehicleDetailsDateRange.Create(null, null);
        var startUtc = Utc("2026-05-01T08:00:00Z");
        var endUtc = Utc("2026-05-01T10:00:00Z");

        Assert.IsTrue(range.IsValid);
        Assert.IsTrue(range.Overlaps(startUtc, endUtc));
        Assert.AreEqual(startUtc, range.ClipStart(startUtc));
        Assert.AreEqual(endUtc, range.ClipEnd(endUtc));
    }

    [TestMethod]
    public void Overlaps_WithFromAndTo_UsesExclusiveNextDayBoundary()
    {
        var range = VehicleDetailsDateRange.Create(
            DateOnly.FromDateTime(Utc("2026-05-10T00:00:00Z")),
            DateOnly.FromDateTime(Utc("2026-05-12T00:00:00Z")));

        Assert.IsFalse(range.Overlaps(
            Utc("2026-05-09T08:00:00Z"),
            Utc("2026-05-10T00:00:00Z")));
        Assert.IsTrue(range.Overlaps(
            Utc("2026-05-09T23:00:00Z"),
            Utc("2026-05-10T01:00:00Z")));
        Assert.IsTrue(range.Overlaps(
            Utc("2026-05-12T23:00:00Z"),
            Utc("2026-05-13T01:00:00Z")));
        Assert.IsFalse(range.Overlaps(
            Utc("2026-05-13T00:00:00Z"),
            Utc("2026-05-13T01:00:00Z")));
    }

    [TestMethod]
    public void ClipStartAndEnd_TrimsActivityCrossingRangeBoundaries()
    {
        var range = VehicleDetailsDateRange.Create(
            DateOnly.FromDateTime(Utc("2026-05-10T00:00:00Z")),
            DateOnly.FromDateTime(Utc("2026-05-12T00:00:00Z")));

        var clippedStart = range.ClipStart(Utc("2026-05-09T23:00:00Z"));
        var clippedEnd = range.ClipEnd(Utc("2026-05-13T01:00:00Z"));

        Assert.AreEqual(Utc("2026-05-10T00:00:00Z"), clippedStart);
        Assert.AreEqual(Utc("2026-05-13T00:00:00Z"), clippedEnd);
    }

    [TestMethod]
    public void Create_WhenFromIsAfterTo_ReturnsInvalidRange()
    {
        var range = VehicleDetailsDateRange.Create(
            DateOnly.FromDateTime(Utc("2026-05-13T00:00:00Z")),
            DateOnly.FromDateTime(Utc("2026-05-12T00:00:00Z")));

        Assert.IsFalse(range.IsValid);
    }

    private static DateTime Utc(string value) =>
        DateTime.Parse(value).ToUniversalTime();
}