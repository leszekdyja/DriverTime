using DriverTime.Application.Compliance.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Compliance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class ComplianceEvaluationCleanupTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OtherCompanyId = Guid.NewGuid();
    private static readonly Guid DriverId = Guid.NewGuid();
    private static readonly Guid OtherDriverId = Guid.NewGuid();
    private static readonly string[] ReplaceableCodes = ["DAILY_REST"];
    private static readonly ComplianceEvaluationService.ReplacementRange ReplacementRange = new(
        new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc));

    [TestMethod]
    public void ShouldDeleteExistingViolation_SameDriverCompanyAndRange_ReturnsTrue()
    {
        var violation = Violation(
            CompanyId,
            DriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");

        var shouldDelete = ComplianceEvaluationService.ShouldDeleteExistingViolation(
            violation,
            CompanyId,
            DriverId,
            ReplacementRange,
            ReplaceableCodes);

        Assert.IsTrue(shouldDelete);
    }

    [TestMethod]
    public void ShouldDeleteExistingViolation_OtherDriver_ReturnsFalse()
    {
        var violation = Violation(
            CompanyId,
            OtherDriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");

        var shouldDelete = ComplianceEvaluationService.ShouldDeleteExistingViolation(
            violation,
            CompanyId,
            DriverId,
            ReplacementRange,
            ReplaceableCodes);

        Assert.IsFalse(shouldDelete);
    }

    [TestMethod]
    public void ShouldDeleteExistingViolation_SameCompanyOutsideRange_ReturnsFalse()
    {
        var violation = Violation(
            CompanyId,
            DriverId,
            "DAILY_REST",
            "2026-06-10T08:00:00Z",
            "2026-06-10T09:00:00Z");

        var shouldDelete = ComplianceEvaluationService.ShouldDeleteExistingViolation(
            violation,
            CompanyId,
            DriverId,
            ReplacementRange,
            ReplaceableCodes);

        Assert.IsFalse(shouldDelete);
    }

    [TestMethod]
    public void ShouldDeleteExistingViolation_OtherCompany_ReturnsFalse()
    {
        var violation = Violation(
            OtherCompanyId,
            DriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");

        var shouldDelete = ComplianceEvaluationService.ShouldDeleteExistingViolation(
            violation,
            CompanyId,
            DriverId,
            ReplacementRange,
            ReplaceableCodes);

        Assert.IsFalse(shouldDelete);
    }

    [TestMethod]
    public void ResolveReplacementRange_NoNewViolations_UsesTimelineRange()
    {
        var preview = new CompliancePreviewResponseDto
        {
            Timeline =
            [
                new ComplianceTimelineEntryDto
                {
                    StartUtc = ReplacementRange.StartUtc,
                    EndUtc = ReplacementRange.EndUtc
                }
            ]
        };

        var range = ComplianceEvaluationService.ResolveReplacementRange(preview);

        Assert.IsNotNull(range);
        Assert.AreEqual(ReplacementRange.StartUtc, range.StartUtc);
        Assert.AreEqual(ReplacementRange.EndUtc, range.EndUtc);
    }

    [TestMethod]
    public void RecalculationWithoutNewViolations_RemovesStaleViolationFromQueryScope()
    {
        var staleViolation = Violation(
            CompanyId,
            DriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");
        var remainingViolations = new[] { staleViolation }
            .Where(x => !ComplianceEvaluationService.ShouldDeleteExistingViolation(
                x,
                CompanyId,
                DriverId,
                ReplacementRange,
                ReplaceableCodes))
            .ToList();

        var queryResult = remainingViolations
            .Where(x =>
                x.DriverId == DriverId &&
                x.Driver?.CompanyId == CompanyId &&
                x.ViolationEnd >= ReplacementRange.StartUtc &&
                x.ViolationStart <= ReplacementRange.EndUtc)
            .ToList();

        Assert.AreEqual(0, queryResult.Count);
    }

    private static Violation Violation(
        Guid companyId,
        Guid driverId,
        string code,
        string startUtc,
        string endUtc)
    {
        return new Violation
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            Driver = new Driver
            {
                Id = driverId,
                CompanyId = companyId
            },
            RegulationReference = code,
            ViolationType = code,
            Severity = "Warning",
            ViolationStart = DateTime.Parse(startUtc).ToUniversalTime(),
            ViolationEnd = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }
}
