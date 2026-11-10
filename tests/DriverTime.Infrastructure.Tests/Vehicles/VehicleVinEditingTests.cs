using System.ComponentModel.DataAnnotations;
using DriverTime.Api.Controllers;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Vehicles.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Vehicles;

[TestClass]
public class VehicleVinEditingTests
{
    [DataTestMethod]
    [DataRow("WVWZZZ1JZXW000001", true)]
    [DataRow("", false)]
    [DataRow("too-short", false)]
    [DataRow("WVWZZZ1JZQW000001", false)]
    [DataRow("WVWZZZ1JZOW000001", false)]
    [DataRow("WVWZZZ1JZIW000001", false)]
    public void ValidatesVin(string vin, bool expected)
    {
        var request = new UpdateVehicleVinDto { Vin = vin };
        Assert.AreEqual(expected, Validator.TryValidateObject(request,
            new ValidationContext(request), new List<ValidationResult>(), true));
    }

    [DataTestMethod]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    public async Task ScopedUserCanOnlyEditOwnVehicle(bool sameTenant, bool sameScope, bool permitted)
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        var scope = Guid.NewGuid();
        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "DTR12345",
            CompanyId = sameTenant ? tenant : Guid.NewGuid(),
            OperatingCompanyId = sameScope ? scope : Guid.NewGuid(), Vin = "WVWZZZ1JZXW000002" };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        var result = await new VehiclesController(db, new User(tenant, scope))
            .UpdateVin(vehicle.Id, new UpdateVehicleVinDto { Vin = "WVWZZZ1JZXW000001" }, default);
        Assert.AreEqual(permitted, result.Result is OkObjectResult);
        Assert.AreEqual(permitted ? "WVWZZZ1JZXW000001" : "WVWZZZ1JZXW000002", vehicle.Vin);
        if (!permitted) Assert.IsInstanceOfType(result.Result, typeof(NotFoundResult));
    }

    [TestMethod]
    public async Task UnauthenticatedUserCannotEditVin()
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var result = await new VehiclesController(db, new User(Guid.Empty, null))
            .UpdateVin(Guid.NewGuid(), new UpdateVehicleVinDto { Vin = "WVWZZZ1JZXW000001" }, default);
        Assert.IsInstanceOfType(result.Result, typeof(UnauthorizedResult));
    }

    private sealed class User(Guid tenant, Guid? scope) : ICurrentUserService
    {
        public Guid UserId => Guid.Empty;
        public Guid CompanyId => tenant;
        public Guid? OperatingCompanyId => scope;
        public Guid DriverId => Guid.Empty;
        public bool IsMobileDriver => false;
        public bool IsAuthenticated => tenant != Guid.Empty;
    }
}
