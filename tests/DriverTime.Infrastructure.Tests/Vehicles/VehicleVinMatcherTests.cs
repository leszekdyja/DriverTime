using DriverTime.Application.DDD.DTOs;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleVinMatcherTests
{
    [TestMethod]
    public void SameRecordLinksNormalizedPlateToValidatedVin()
    {
        var result = VehicleVinMatcher.UniqueEvidence(new[] {
            new ParsedVehicleUseDto {VehicleRegistration="DTR 12345",VehicleIdentificationNumber="wvwzzz1jzxw000001"},
            new ParsedVehicleUseDto {VehicleRegistration="DTR12345",VehicleIdentificationNumber="WVWZZZ1JZXW000001"}});
        Assert.AreEqual("WVWZZZ1JZXW000001", result["DTR12345"]);
        Assert.AreEqual(string.Empty, VehicleVinMatcher.NormalizeVin("not-a-vin"));
        Assert.AreEqual(string.Empty, VehicleVinMatcher.NormalizeVin("WVWZZZ1JZQW000001"));
    }

    [TestMethod]
    public void ConflictingVinOrRegistrationDoesNotCreateAnAutomaticLink()
    {
        Assert.AreEqual(0, VehicleVinMatcher.UniqueEvidence(new[] {
            new ParsedVehicleUseDto {VehicleRegistration="DTR12345",VehicleIdentificationNumber="WVWZZZ1JZXW000001"},
            new ParsedVehicleUseDto {VehicleRegistration="DTR12345",VehicleIdentificationNumber="WVWZZZ1JZXW000002"}}).Count);
        Assert.AreEqual(0, VehicleVinMatcher.UniqueEvidence(new[] {
            new ParsedVehicleUseDto {VehicleRegistration="DTR12345",VehicleIdentificationNumber="WVWZZZ1JZXW000001"},
            new ParsedVehicleUseDto {VehicleRegistration="DTR54321",VehicleIdentificationNumber="WVWZZZ1JZXW000001"}}).Count);
    }
}
