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

    [TestMethod]
    public void ConfirmImport_CreatesNewDuty()
    {
        var companyId = Guid.NewGuid();
        var duties = new List<PlanningDuty>();

        var result = PlanningDutyService.ConfirmImportForCompany(
            duties,
            CreateConfirmRequest(),
            companyId,
            DateTime.UtcNow);

        Assert.AreEqual(1, result.CreatedCount);
        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual(companyId, duties[0].CompanyId);
        Assert.AreEqual("101", duties[0].DutyNumber);
        Assert.AreEqual("K-11", duties[0].Lines.Single().LineCode);
    }

    [TestMethod]
    public void ConfirmImport_SameDutySecondTime_ReturnsUnchangedAndDoesNotDuplicate()
    {
        var companyId = Guid.NewGuid();
        var duties = new List<PlanningDuty>();
        var request = CreateConfirmRequest();

        PlanningDutyService.ConfirmImportForCompany(duties, request, companyId, DateTime.UtcNow);
        var result = PlanningDutyService.ConfirmImportForCompany(duties, request, companyId, DateTime.UtcNow.AddMinutes(1));

        Assert.AreEqual(1, result.UnchangedCount);
        Assert.AreEqual(1, duties.Count);
    }

    [TestMethod]
    public void ConfirmImport_UpdatesExistingDutyWhenDataDiffers()
    {
        var companyId = Guid.NewGuid();
        var duties = new List<PlanningDuty>();
        var request = CreateConfirmRequest();
        PlanningDutyService.ConfirmImportForCompany(duties, request, companyId, DateTime.UtcNow);

        var updatedRequest = CreateConfirmRequest();
        updatedRequest.Duties[0].DutyName = "Służba 101 zmieniona";
        updatedRequest.Duties[0].WorkingMinutes = 510;

        var result = PlanningDutyService.ConfirmImportForCompany(duties, updatedRequest, companyId, DateTime.UtcNow.AddMinutes(1));

        Assert.AreEqual(1, result.UpdatedCount);
        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("Służba 101 zmieniona", duties[0].Name);
        Assert.AreEqual(510, duties[0].WorkMinutes);
    }

    [TestMethod]
    public void ConfirmImport_ValidatesMissingRequiredFields()
    {
        var request = CreateConfirmRequest();
        request.Duties[0].DutyNumber = "";
        request.Duties[0].StartTime = null;
        request.Duties[0].EndTime = null;

        var exception = Assert.ThrowsException<PlanningDutyValidationException>(() =>
            PlanningDutyService.ConfirmImportForCompany(new List<PlanningDuty>(), request, Guid.NewGuid(), DateTime.UtcNow));

        Assert.IsTrue(exception.Errors.Any(x => x.Contains("numer służby jest wymagany", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(exception.Errors.Any(x => x.Contains("godzina rozpoczęcia jest wymagana", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(exception.Errors.Any(x => x.Contains("godzina zakończenia jest wymagana", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ConfirmImport_DoesNotUpdateDutyFromOtherCompany()
    {
        var currentCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var duties = new List<PlanningDuty>
        {
            CreateExistingDuty(otherCompanyId)
        };

        var result = PlanningDutyService.ConfirmImportForCompany(
            duties,
            CreateConfirmRequest(),
            currentCompanyId,
            DateTime.UtcNow);

        Assert.AreEqual(1, result.CreatedCount);
        Assert.AreEqual(2, duties.Count);
        Assert.AreEqual(otherCompanyId, duties[0].CompanyId);
        Assert.AreEqual(currentCompanyId, duties[1].CompanyId);
    }


    [TestMethod]
    public void ConfirmImport_TransportDutySheetData_SavesCompleteDutyDefinition()
    {
        var companyId = Guid.NewGuid();
        var duties = new List<PlanningDuty>();
        var request = new PlanningDutyPdfImportConfirmRequestDto
        {
            SourceFileName = "sluzba-60.pdf",
            Duties = new List<PlanningDutyPdfImportConfirmItemDto>
            {
                new()
                {
                    DutyNumber = "60",
                    DutyName = "Służba 60",
                    Line = "K-64 / K-48",
                    ValidFrom = new DateOnly(2025, 11, 1),
                    VehicleRequirement = "Autobus 41 miejscowy",
                    StartTime = new TimeOnly(16, 20),
                    EndTime = new TimeOnly(3, 5),
                    WorkingMinutes = 540,
                    BreakMinutes = 120,
                    DistanceKm = 218m,
                    Notes = "Ważna od 01.11.2025; Autobus 41 miejscowy",
                    Stops = new List<PlanningDutyPdfImportConfirmStopDto>
                    {
                        new() { Sequence = 1, StopName = "BAZA WPO", Km = 0m, DepartureTime = new TimeOnly(16, 20) },
                        new() { Sequence = 2, StopName = "ZG RUDNA ZACHODNIA", Km = 56m, ArrivalTime = new TimeOnly(18, 10), DepartureTime = new TimeOnly(18, 12) },
                        new() { Sequence = 3, StopName = "SZYB SG", Km = 74m, DepartureTime = new TimeOnly(18, 40) }
                    }
                }
            }
        };

        var result = PlanningDutyService.ConfirmImportForCompany(duties, request, companyId, DateTime.UtcNow);

        Assert.AreEqual(1, result.CreatedCount);
        var duty = duties.Single();
        Assert.AreEqual("60", duty.DutyNumber);
        Assert.AreEqual(new DateOnly(2025, 11, 1), duty.ValidFrom);
        Assert.AreEqual("Autobus 41 miejscowy", duty.VehicleRequirement);
        Assert.AreEqual(218m, duty.DistanceKm);
        Assert.IsTrue(duty.Lines.Any(x => x.LineCode == "K-64"));
        Assert.IsTrue(duty.Lines.Any(x => x.LineCode == "K-48"));
        Assert.AreEqual(3, duty.Stops.Count);
        Assert.AreEqual("BAZA WPO", duty.Stops.OrderBy(x => x.Sequence).First().StopName);
        Assert.AreEqual(56m, duty.Stops.Single(x => x.StopName == "ZG RUDNA ZACHODNIA").Km);
        Assert.AreEqual(3, duty.Stops.Single(x => x.StopName == "SZYB SG").Sequence);
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

    private static PlanningDutyPdfImportConfirmRequestDto CreateConfirmRequest() => new()
    {
        SourceFileName = "sluzby.pdf",
        Duties = new List<PlanningDutyPdfImportConfirmItemDto>
        {
            new()
            {
                DutyNumber = "101",
                DutyName = "Służba 101",
                Line = "K-11",
                StartTime = new TimeOnly(5, 20),
                EndTime = new TimeOnly(13, 45),
                WorkingMinutes = 505,
                DrivingMinutes = 320,
                BreakMinutes = 45,
                DistanceKm = 112.5m,
                Notes = "Import testowy",
                Stops = new List<PlanningDutyPdfImportConfirmStopDto>
                {
                    new()
                    {
                        Sequence = 1,
                        StopName = "Dworzec",
                        DepartureTime = new TimeOnly(5, 20)
                    }
                }
            }
        }
    };

    private static PlanningDuty CreateExistingDuty(Guid companyId)
    {
        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DutyNumber = "101",
            Name = "Służba 101 obca",
            StartTime = new TimeOnly(5, 20),
            EndTime = new TimeOnly(13, 45),
            CreatedAtUtc = DateTime.UtcNow
        };
        duty.Lines.Add(new PlanningDutyLine
        {
            Id = Guid.NewGuid(),
            PlanningDutyId = duty.Id,
            LineCode = "K-11"
        });

        return duty;
    }
}

