using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningDutyServiceTests
{
    [TestMethod]
    public void CreateDutyForCompany_AssignsCurrentCompanyId()
    {
        var companyId = Guid.NewGuid();
        var request = CreateValidRequest();

        var duty = PlanningDutyService.CreateDutyForCompany(request, companyId, DateTime.UtcNow);

        Assert.AreEqual(companyId, duty.CompanyId);
        Assert.AreEqual("24", duty.DutyNumber);
        Assert.AreEqual("Służba 24", duty.Name);
    }

    [TestMethod]
    public void IsInCompanyScope_ReturnsTrueOnlyForCurrentCompany()
    {
        var companyId = Guid.NewGuid();
        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DutyNumber = "24",
            Name = "Służba 24"
        };

        Assert.IsTrue(PlanningDutyService.IsInCompanyScope(duty, companyId));
        Assert.IsFalse(PlanningDutyService.IsInCompanyScope(duty, Guid.NewGuid()));
    }

    [TestMethod]
    public void ForeignDuty_IsOutsideCompanyScopeSoServiceCanReturnNotFound()
    {
        var currentCompanyId = Guid.NewGuid();
        var foreignDuty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            DutyNumber = "24",
            Name = "Służba 24"
        };

        Assert.IsFalse(PlanningDutyService.IsInCompanyScope(foreignDuty, currentCompanyId));
    }

    [TestMethod]
    public void Validate_ThrowsForEmptyDutyNumberOrName()
    {
        var request = CreateValidRequest();
        request.DutyNumber = "";
        request.Name = " ";

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(
            () => PlanningDutyService.Validate(request));

        CollectionAssert.Contains(exception.Errors.ToList(), "Numer służby jest wymagany.");
        CollectionAssert.Contains(exception.Errors.ToList(), "Nazwa jest wymagana.");
    }

    [TestMethod]
    public void Validate_ThrowsForNegativeMinutesAndDistance()
    {
        var request = CreateValidRequest();
        request.WorkMinutes = -1;
        request.DistanceKm = -0.1m;

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(
            () => PlanningDutyService.Validate(request));

        CollectionAssert.Contains(exception.Errors.ToList(), "Czas pracy nie może być ujemny.");
        CollectionAssert.Contains(exception.Errors.ToList(), "Kilometry nie mogą być ujemne.");
    }

    [TestMethod]
    public void DriverTimeModel_DeletesPlanningDutyLinesAndStopsWithDuty()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseNpgsql("Host=localhost;Database=drivertime;Username=drivetime;Password=postgres")
            .Options;
        using var dbContext = new DriverTimeDbContext(options);

        var lineForeignKey = dbContext.Model.FindEntityType(typeof(PlanningDutyLine))
            ?.GetForeignKeys()
            .SingleOrDefault(x => x.PrincipalEntityType.ClrType == typeof(PlanningDuty));
        var stopForeignKey = dbContext.Model.FindEntityType(typeof(PlanningDutyStop))
            ?.GetForeignKeys()
            .SingleOrDefault(x => x.PrincipalEntityType.ClrType == typeof(PlanningDuty));

        Assert.IsNotNull(lineForeignKey);
        Assert.IsNotNull(stopForeignKey);
        Assert.AreEqual(DeleteBehavior.Cascade, lineForeignKey.DeleteBehavior);
        Assert.AreEqual(DeleteBehavior.Cascade, stopForeignKey.DeleteBehavior);
    }

    private static CreatePlanningDutyRequest CreateValidRequest() => new()
    {
        DutyNumber = "24",
        Name = "Służba 24",
        ValidFrom = new DateOnly(2026, 6, 28),
        VehicleRequirement = "autobus 41 miejscowy",
        StartTime = new TimeOnly(6, 15),
        EndTime = new TimeOnly(14, 40),
        TotalDurationMinutes = 505,
        WorkMinutes = 460,
        BreakMinutes = 45,
        DrivingMinutes = 320,
        DistanceKm = 128.5m,
        Notes = "Test"
    };
}
