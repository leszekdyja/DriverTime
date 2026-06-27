using System.Reflection;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Violations;

[TestClass]
public class ViolationQueryServiceMappingTests
{
    [TestMethod]
    public void Map_DailyRestMetadata_ExposesBusinessScaleFields()
    {
        var violation = CreateViolation(
            "DAILY_REST",
            """{"longestRestMinutes":405,"requiredRegularRestMinutes":660}""");

        var dto = Map(violation);

        Assert.AreEqual(405, dto.ActualValueMinutes);
        Assert.AreEqual(660, dto.RequiredValueMinutes);
        Assert.AreEqual(-255, dto.DifferenceMinutes);
        Assert.AreEqual(255, dto.MissingMinutes);
        Assert.AreEqual("brakuje 4 godz. 15 min", dto.ScaleLabel);
        Assert.IsTrue(dto.BusinessSummary.Contains("Brakowało 4 godz. 15 min", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Map_DailyDrivingMetadata_ExposesExcessScaleFields()
    {
        var violation = CreateViolation(
            "DAILY_DRIVING_LIMIT",
            """{"totalDrivingMinutes":638,"limitMinutes":600,"exceededMinutes":38}""");

        var dto = Map(violation);

        Assert.AreEqual(638, dto.ActualValueMinutes);
        Assert.AreEqual(600, dto.RequiredValueMinutes);
        Assert.AreEqual(38, dto.DifferenceMinutes);
        Assert.AreEqual(38, dto.ExcessMinutes);
        Assert.AreEqual("+38 min", dto.ScaleLabel);
    }

    [TestMethod]
    public void Map_WeeklyRestCompensationMetadata_ExposesCompensationDeadline()
    {
        var violation = CreateViolation(
            "WEEKLY_REST_COMPENSATION",
            """{"reducedRestMinutes":1440,"compensationDebtMinutes":1260,"compensationDeadlineUtc":"2026-07-12T00:00:00Z"}""");

        var dto = Map(violation);

        Assert.AreEqual(1260, dto.CompensationMinutes);
        Assert.AreEqual(new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc), dto.CompensationDeadlineUtc);
        Assert.AreEqual("rekompensata 21 godz.", dto.ScaleLabel);
    }

    private static ViolationDto Map(Violation violation)
    {
        var method = typeof(ViolationQueryService).GetMethod(
            "Map",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Map method not found.");

        return (ViolationDto)method.Invoke(null, [violation])!;
    }

    private static Violation CreateViolation(string code, string metadataJson)
    {
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            FirstName = "Jan",
            LastName = "Kowalski",
            CardNumber = "CARD-1"
        };

        return new Violation
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            Driver = driver,
            RegulationReference = code,
            ViolationType = code,
            Severity = "High",
            DurationMinutes = 0,
            MetadataJson = metadataJson,
            ViolationStart = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc),
            ViolationEnd = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            CalculatedAt = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)
        };
    }
}
