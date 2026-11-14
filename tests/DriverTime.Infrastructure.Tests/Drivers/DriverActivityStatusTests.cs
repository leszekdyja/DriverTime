using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Drivers;

[TestClass]
public class DriverActivityStatusTests
{
    [TestMethod]
    public async Task StatusCanBeChangedAndRestoredWithoutRemovingHistoryOrPlanningPreferences()
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company=Guid.NewGuid();var driver=new Driver {Id=Guid.NewGuid(),CompanyId=company};
        db.Drivers.Add(driver);await db.SaveChangesAsync();
        var service=new DriverService(db,new User(company,null), NullLogger<DriverService>.Instance);
        var saved=await service.UpdatePlanningAsync(driver.Id,new UpdateDriverPlanningDto {IsActive=false,IncludeInPlanning=true});
        Assert.IsFalse(saved!.IsActive);Assert.IsTrue(saved.IncludeInPlanning);
        Assert.IsFalse((await service.GetAllAsync()).Single().IsActive);
        Assert.AreEqual(1,await db.Drivers.CountAsync());
        saved=await service.UpdatePlanningAsync(driver.Id,new UpdateDriverPlanningDto {IsActive=true,IncludeInPlanning=true});
        Assert.IsTrue(saved!.IsActive);
    }

    [TestMethod]
    public async Task CompanyAccountCannotDeactivateDriverFromAnotherCompany()
    {
        await using var db = new DriverTimeDbContext(new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var company=Guid.NewGuid();var driver=new Driver {Id=Guid.NewGuid(),CompanyId=company,OperatingCompanyId=Guid.NewGuid()};
        db.Drivers.Add(driver);await db.SaveChangesAsync();
        var service=new DriverService(db,new User(company,Guid.NewGuid()), NullLogger<DriverService>.Instance);
        Assert.IsNull(await service.UpdatePlanningAsync(driver.Id,new UpdateDriverPlanningDto {IsActive=false}));
        Assert.IsTrue(driver.IsActive);
    }

    private sealed class User(Guid company,Guid? scope):ICurrentUserService
    {
        public Guid UserId=>Guid.NewGuid();public Guid CompanyId=>company;public Guid? OperatingCompanyId=>scope;
        public Guid DriverId=>Guid.Empty;public bool IsMobileDriver=>false;public bool IsAuthenticated=>true;
    }
}
