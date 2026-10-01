using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleActivityDriverAssignmentTests
{
    [TestMethod]
    public void ResolveDriverName_UsesOnlyOverlappingVehicleUseDriver()
    {
        var firstDriverId = Guid.NewGuid();
        var secondDriverId = Guid.NewGuid();
        var firstUse = VehicleUse(firstDriverId, "Jan", "Nowak", "2024-06-08T08:00:00Z", "2024-06-08T10:00:00Z");
        var secondUse = VehicleUse(secondDriverId, "Anna", "Kowalska", "2024-06-08T14:00:00Z", "2024-06-08T16:00:00Z");

        var firstName = VehicleActivityDriverAssignment.ResolveDriverName(
            Utc("2024-06-08T08:30:00Z"),
            Utc("2024-06-08T09:00:00Z"),
            firstUse,
            FormatDriverName);
        var secondName = VehicleActivityDriverAssignment.ResolveDriverName(
            Utc("2024-06-08T14:30:00Z"),
            Utc("2024-06-08T15:00:00Z"),
            secondUse,
            FormatDriverName);
        var outsideName = VehicleActivityDriverAssignment.ResolveDriverName(
            Utc("2024-06-08T11:00:00Z"),
            Utc("2024-06-08T11:30:00Z"),
            firstUse,
            FormatDriverName);

        Assert.AreEqual("Nowak Jan", firstName);
        Assert.AreEqual("Kowalska Anna", secondName);
        Assert.IsNull(outsideName);
    }

    private static VehicleUse VehicleUse(Guid driverId, string firstName, string lastName, string startUtc, string endUtc)
    {
        return new VehicleUse
        {
            DddFile = new DddFile
            {
                DriverId = driverId,
                Driver = new Driver
                {
                    Id = driverId,
                    FirstName = firstName,
                    LastName = lastName
                }
            },
            RegistrationNumber = "DPL07551",
            StartUtc = Utc(startUtc),
            EndUtc = Utc(endUtc)
        };
    }

    private static string FormatDriverName(string firstName, string lastName) =>
        string.Join(" ", new[] { lastName, firstName }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static DateTime Utc(string value) =>
        DateTime.Parse(value).ToUniversalTime();
}
