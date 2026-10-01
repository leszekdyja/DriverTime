using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleUseDateValidatorTests
{
    private static readonly DateTime NowUtc = new(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void ValidVehicleUseWithinReasonableRange_ReturnsTrue()
    {
        var startUtc = new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 6, 19, 10, 0, 0, DateTimeKind.Utc);

        Assert.IsTrue(VehicleUseDateValidator.IsValid(startUtc, endUtc, NowUtc));
    }

    [TestMethod]
    public void StartBeforeYear2000_ReturnsFalse()
    {
        var startUtc = new DateTime(1999, 12, 31, 23, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2000, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        Assert.IsFalse(VehicleUseDateValidator.IsValid(startUtc, endUtc, NowUtc));
    }

    [TestMethod]
    public void EndNotAfterStart_ReturnsFalse()
    {
        var startUtc = new DateTime(2026, 6, 19, 8, 0, 0, DateTimeKind.Utc);

        Assert.IsFalse(VehicleUseDateValidator.IsValid(startUtc, startUtc, NowUtc));
    }

    [TestMethod]
    public void StartMoreThanOneDayInFuture_ReturnsFalse()
    {
        var startUtc = NowUtc.AddDays(1).AddMinutes(1);
        var endUtc = startUtc.AddHours(1);

        Assert.IsFalse(VehicleUseDateValidator.IsValid(startUtc, endUtc, NowUtc));
    }

    [TestMethod]
    public void EndMoreThanOneDayInFuture_ReturnsFalse()
    {
        var startUtc = NowUtc;
        var endUtc = NowUtc.AddDays(1).AddMinutes(1);

        Assert.IsFalse(VehicleUseDateValidator.IsValid(startUtc, endUtc, NowUtc));
    }
}
