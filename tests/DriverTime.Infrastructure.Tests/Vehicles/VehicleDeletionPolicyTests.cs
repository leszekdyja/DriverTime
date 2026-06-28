using DriverTime.Application.Vehicles;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleDeletionPolicyTests
{
    [TestMethod]
    public void CanDelete_VehicleFromCurrentCompany_ReturnsTrue()
    {
        var companyId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            RegistrationNumber = "DPL 07511"
        };

        Assert.IsTrue(VehicleDeletionPolicy.CanDelete(vehicle, companyId));
    }

    [TestMethod]
    public void CanDelete_VehicleFromOtherCompany_ReturnsFalse()
    {
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            RegistrationNumber = "DPL 07511"
        };

        Assert.IsFalse(VehicleDeletionPolicy.CanDelete(vehicle, Guid.NewGuid()));
    }

    [TestMethod]
    public void CanDelete_NonExistingVehicle_ReturnsFalse()
    {
        Assert.IsFalse(VehicleDeletionPolicy.CanDelete(null, Guid.NewGuid()));
    }

    [TestMethod]
    public void VehicleUseModel_DoesNotReferenceVehicle_SoHistoryIsNotDeletedWithVehicle()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseNpgsql("Host=localhost;Database=drivertime;Username=drivertime;Password=postgres")
            .Options;
        using var dbContext = new DriverTimeDbContext(options);

        var vehicleUseEntity = dbContext.Model.FindEntityType(typeof(VehicleUse));
        var vehicleForeignKeys = vehicleUseEntity?.GetForeignKeys()
            .Where(x => x.PrincipalEntityType.ClrType == typeof(Vehicle))
            .ToList();

        Assert.IsNotNull(vehicleUseEntity);
        Assert.AreEqual(0, vehicleForeignKeys?.Count ?? -1);
    }

    [TestMethod]
    public void DriverActivityVehicleRelation_IsNullableSoActivitiesCanRemainAfterVehicleDeletion()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseNpgsql("Host=localhost;Database=drivertime;Username=drivertime;Password=postgres")
            .Options;
        using var dbContext = new DriverTimeDbContext(options);

        var driverActivityEntity = dbContext.Model.FindEntityType(typeof(DriverActivity));
        var vehicleForeignKey = driverActivityEntity?.GetForeignKeys()
            .SingleOrDefault(x => x.PrincipalEntityType.ClrType == typeof(Vehicle));

        Assert.IsNotNull(driverActivityEntity);
        Assert.IsNotNull(vehicleForeignKey);
        Assert.IsFalse(vehicleForeignKey.IsRequired);
        Assert.AreEqual(DeleteBehavior.ClientSetNull, vehicleForeignKey.DeleteBehavior);
    }
}

