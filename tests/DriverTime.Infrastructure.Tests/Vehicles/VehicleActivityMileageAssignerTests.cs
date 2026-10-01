using DriverTime.Application.Vehicles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleActivityMileageAssignerTests
{
    [TestMethod]
    public void Assign_OneDrivingActivityCoveringWholeVehicleUse_ReturnsMileageForVehicleDetailsActivity()
    {
        var activityId = Guid.NewGuid();
        var vehicleUseId = Guid.NewGuid();
        var assignments = VehicleActivityMileageAssigner.Assign(new[]
        {
            new VehicleActivityMileageSource(
                activityId,
                vehicleUseId,
                "DPL 07511",
                "DRIVING",
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                722376,
                722457,
                81)
        });

        var key = VehicleActivityMileageAssigner.BuildAssignmentKey(activityId, vehicleUseId);

        Assert.IsTrue(assignments.ContainsKey(key));
        Assert.AreEqual(722376, assignments[key].StartOdometerKm);
        Assert.AreEqual(722457, assignments[key].EndOdometerKm);
        Assert.AreEqual(81, assignments[key].DistanceKm);
    }

    [TestMethod]
    public void Assign_OneVehicleUseWithMultipleDrivingActivities_DoesNotAssignWholeVehicleUseMileageToAnySingleActivity()
    {
        var firstActivityId = Guid.NewGuid();
        var secondActivityId = Guid.NewGuid();
        var vehicleUseId = Guid.NewGuid();
        var assignments = VehicleActivityMileageAssigner.Assign(new[]
        {
            new VehicleActivityMileageSource(
                firstActivityId,
                vehicleUseId,
                "DPL 48803",
                "DRIVING",
                Utc("2026-05-29T00:18:00Z"),
                Utc("2026-05-29T00:55:00Z"),
                Utc("2026-05-29T00:10:00Z"),
                Utc("2026-05-29T02:30:00Z"),
                58876,
                58985,
                109),
            new VehicleActivityMileageSource(
                secondActivityId,
                vehicleUseId,
                "DPL 48803",
                "DRIVING",
                Utc("2026-05-29T01:30:00Z"),
                Utc("2026-05-29T02:20:00Z"),
                Utc("2026-05-29T00:10:00Z"),
                Utc("2026-05-29T02:30:00Z"),
                58876,
                58985,
                109)
        });

        Assert.AreEqual(0, assignments.Count);
    }

    [TestMethod]
    public void Assign_OneDrivingActivityInsideLongerVehicleUse_DoesNotAssignWholeVehicleUseMileage()
    {
        var assignments = VehicleActivityMileageAssigner.Assign(new[]
        {
            new VehicleActivityMileageSource(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "DPL 48803",
                "DRIVING",
                Utc("2026-05-29T00:18:00Z"),
                Utc("2026-05-29T00:55:00Z"),
                Utc("2026-05-29T00:10:00Z"),
                Utc("2026-05-29T02:30:00Z"),
                58876,
                58985,
                109)
        });

        Assert.AreEqual(0, assignments.Count);
    }

    [TestMethod]
    public void Assign_NonDrivingActivity_DoesNotReturnMileage()
    {
        var assignments = VehicleActivityMileageAssigner.Assign(new[]
        {
            new VehicleActivityMileageSource(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "DPL 07511",
                "WORK",
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                722376,
                722457,
                81)
        });

        Assert.AreEqual(0, assignments.Count);
    }

    [TestMethod]
    public void Assign_DuplicateWholeVehicleUseDrivingActivities_ReturnsMileageOnlyOnceWhenAmbiguousDuplicateExists()
    {
        var firstActivityId = Guid.NewGuid();
        var secondActivityId = Guid.NewGuid();
        var vehicleUseId = Guid.NewGuid();
        var assignments = VehicleActivityMileageAssigner.Assign(new[]
        {
            new VehicleActivityMileageSource(
                firstActivityId,
                vehicleUseId,
                "DPL 07511",
                "DRIVING",
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                722376,
                722457,
                81),
            new VehicleActivityMileageSource(
                secondActivityId,
                vehicleUseId,
                "DPL 07511",
                "DRIVING",
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                Utc("2026-06-11T18:45:19Z"),
                Utc("2026-06-11T21:12:37Z"),
                722376,
                722457,
                81)
        });

        Assert.AreEqual(0, assignments.Count);
    }

    private static DateTime Utc(string value) =>
        DateTime.Parse(value).ToUniversalTime();
}
