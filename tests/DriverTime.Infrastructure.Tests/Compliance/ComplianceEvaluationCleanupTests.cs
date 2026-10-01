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


    [TestMethod]
    public void ShouldDeleteExistingViolationForManualRecalculation_SameDriverAndCompany_ReturnsTrue()
    {
        var violation = Violation(
            CompanyId,
            DriverId,
            "REDUCED_WEEKLY_REST",
            "2026-06-15T02:00:00Z",
            "2026-06-15T02:00:00Z");

        var shouldDelete = ComplianceEvaluationService.ShouldDeleteExistingViolationForManualRecalculation(
            violation,
            CompanyId,
            DriverId);

        Assert.IsTrue(shouldDelete);
    }

    [TestMethod]
    public void ShouldDeleteExistingViolationForManualRecalculation_OtherCompany_ReturnsFalse()
    {
        var violation = Violation(
            OtherCompanyId,
            DriverId,
            "REDUCED_WEEKLY_REST",
            "2026-06-15T02:00:00Z",
            "2026-06-15T02:00:00Z");

        var shouldDelete = ComplianceEvaluationService.ShouldDeleteExistingViolationForManualRecalculation(
            violation,
            CompanyId,
            DriverId);

        Assert.IsFalse(shouldDelete);
    }

    [TestMethod]
    public void BuildManualRecalculationSnapshot_RemovesStaleDriverViolationsAndKeepsCurrentOnly()
    {
        var staleDailyRest = Violation(
            CompanyId,
            DriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");
        var staleWeeklyRest = Violation(
            CompanyId,
            DriverId,
            "REDUCED_WEEKLY_REST",
            "2026-06-15T02:00:00Z",
            "2026-06-15T02:00:00Z");
        var otherDriverViolation = Violation(
            CompanyId,
            OtherDriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");
        var otherCompanyViolation = Violation(
            OtherCompanyId,
            DriverId,
            "DAILY_REST",
            "2026-06-08T08:00:00Z",
            "2026-06-08T09:00:00Z");
        var currentViolation = Violation(
            CompanyId,
            DriverId,
            "CONTINUOUS_DRIVING_BREAK",
            "2026-06-08T12:00:00Z",
            "2026-06-08T12:30:00Z");

        var snapshot = ComplianceEvaluationService.BuildManualRecalculationSnapshot(
            [staleDailyRest, staleWeeklyRest, otherDriverViolation, otherCompanyViolation],
            CompanyId,
            DriverId,
            [currentViolation]);

        CollectionAssert.DoesNotContain(snapshot.ToList(), staleDailyRest);
        CollectionAssert.DoesNotContain(snapshot.ToList(), staleWeeklyRest);
        CollectionAssert.Contains(snapshot.ToList(), otherDriverViolation);
        CollectionAssert.Contains(snapshot.ToList(), otherCompanyViolation);
        CollectionAssert.Contains(snapshot.ToList(), currentViolation);
        Assert.AreEqual(3, snapshot.Count);
    }

    [TestMethod]
    public void MapViolation_ForManualRecalculation_CreatesCurrentPersistedViolation()
    {
        var previewViolation = new ComplianceViolationPreviewDto
        {
            Code = "CONTINUOUS_DRIVING_BREAK",
            RuleName = "Przerwa po 4h30 jazdy",
            Severity = "HIGH",
            ActualMinutes = 286,
            PeriodStartUtc = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc = new DateTime(2026, 6, 8, 12, 46, 0, DateTimeKind.Utc),
            Metadata = new Dictionary<string, object>
            {
                ["source"] = "test"
            }
        };
        var calculatedAt = new DateTime(2026, 6, 8, 13, 0, 0, DateTimeKind.Utc);

        var violation = ComplianceEvaluationService.MapViolation(
            DriverId,
            previewViolation,
            calculatedAt);

        Assert.AreEqual(DriverId, violation.DriverId);
        Assert.AreEqual("CONTINUOUS_DRIVING_BREAK", violation.RegulationReference);
        Assert.AreEqual("Critical", violation.Severity);
        Assert.AreEqual(286, violation.DurationMinutes);
        Assert.AreEqual(previewViolation.PeriodStartUtc, violation.ViolationStart);
        Assert.AreEqual(previewViolation.PeriodEndUtc, violation.ViolationEnd);
        Assert.AreEqual(calculatedAt, violation.CalculatedAt);
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
