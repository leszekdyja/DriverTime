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
    public void Map_ContinuousDrivingBreakMetadata_IncludesFortyFiveMinuteBreakAction()
    {
        var violation = CreateViolation(
            "CONTINUOUS_DRIVING_BREAK",
            """{"continuousDrivingMinutes":286,"requiredBreakMinutes":45,"receivedBreakMinutes":12,"exceededMinutes":16}""");

        var dto = Map(violation);

        Assert.IsNotNull(dto.DispatcherRecommendation);
        Assert.IsTrue(dto.DispatcherRecommendation.RecommendedActions.Any(x => x.Contains("45 minut", StringComparison.Ordinal)));
        Assert.IsTrue(dto.DispatcherRecommendation.Summary.Contains("przerwy", StringComparison.Ordinal));
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
        Assert.IsNotNull(dto.DispatcherRecommendation);
        Assert.IsTrue(dto.DispatcherRecommendation.Summary.Contains("12.07.2026", StringComparison.Ordinal));
        Assert.IsTrue(dto.DispatcherRecommendation.RecommendedActions.Any(x => x.Contains("12.07.2026", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Map_MissingEndCountryViolation_ReturnsDispatcherRecommendation()
    {
        var violation = CreateViolation(
            "MISSING_END_COUNTRY",
            "{}"
        );

        var dto = Map(violation);

        Assert.IsNotNull(dto.DispatcherRecommendation);
        Assert.AreEqual("WARNING", dto.DispatcherRecommendation.Status);
        Assert.IsTrue(dto.DispatcherRecommendation.CanDrive);
        Assert.IsTrue(dto.DispatcherRecommendation.CanStartShift);
        Assert.IsTrue(dto.DispatcherRecommendation.PlannerAttentionRequired);
        Assert.IsTrue(dto.DispatcherRecommendation.Summary.Contains("kraju zakończenia", StringComparison.Ordinal));
        Assert.IsTrue(dto.DispatcherRecommendation.RecommendedActions.Any(x => x.Contains("kraj zakończenia", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Map_MissingStartCountryViolation_ReturnsDispatcherRecommendation()
    {
        var violation = CreateViolation(
            "MISSING_START_COUNTRY",
            "{}"
        );

        var dto = Map(violation);

        Assert.IsNotNull(dto.DispatcherRecommendation);
        Assert.AreEqual("WARNING", dto.DispatcherRecommendation.Status);
        Assert.IsTrue(dto.DispatcherRecommendation.CanDrive);
        Assert.IsTrue(dto.DispatcherRecommendation.CanStartShift);
        Assert.IsTrue(dto.DispatcherRecommendation.PlannerAttentionRequired);
        Assert.IsTrue(dto.DispatcherRecommendation.Summary.Contains("kraju rozpoczęcia", StringComparison.Ordinal));
        Assert.IsTrue(dto.DispatcherRecommendation.RecommendedActions.Any(x => x.Contains("kraj rozpoczęcia", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Map_GenericCountryDataViolation_ReturnsDataQualityRecommendation()
    {
        var violation = CreateViolation(
            "INCOMPLETE_COUNTRY_DATA",
            "{}"
        );

        var dto = Map(violation);

        Assert.IsNotNull(dto.DispatcherRecommendation);
        Assert.AreEqual("WARNING", dto.DispatcherRecommendation.Status);
        Assert.IsTrue(dto.DispatcherRecommendation.CanDrive);
        Assert.IsTrue(dto.DispatcherRecommendation.CanStartShift);
        Assert.IsTrue(dto.DispatcherRecommendation.PlannerAttentionRequired);
        Assert.IsTrue(dto.DispatcherRecommendation.Summary.Contains("kompletnością danych tachografu", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Map_UnknownViolationType_ReturnsSafeFallbackRecommendation()
    {
        var violation = CreateViolation(
            "UNKNOWN_RULE",
            "{}"
        );

        var dto = Map(violation);

        Assert.IsNotNull(dto.DispatcherRecommendation);
        Assert.AreEqual("WARNING", dto.DispatcherRecommendation.Status);
        Assert.IsTrue(dto.DispatcherRecommendation.CanDrive);
        Assert.IsTrue(dto.DispatcherRecommendation.CanStartShift);
        Assert.IsTrue(dto.DispatcherRecommendation.RecommendedActions.Count > 0);
    }



    [TestMethod]
    public void Map_ContinuousDrivingBreakMetadata_BuildsRuleAnalysis()
    {
        var violation = CreateViolation(
            "CONTINUOUS_DRIVING_BREAK",
            """{"continuousDrivingMinutes":292,"requiredBreakMinutes":45,"receivedBreakMinutes":15,"exceededMinutes":22}""");
        var activities = new[]
        {
            Activity(violation, "DRIVING", "2026-07-01T08:00:00Z", "2026-07-01T10:00:00Z"),
            Activity(violation, "REST", "2026-07-01T10:00:00Z", "2026-07-01T10:15:00Z"),
            Activity(violation, "DRIVING", "2026-07-01T10:15:00Z", "2026-07-01T13:07:00Z")
        };

        var dto = Map(violation, activities);

        Assert.IsNotNull(dto.RuleAnalysis);
        Assert.AreEqual("Przerwa po 4h30 jazdy", dto.RuleAnalysis.RuleName);
        Assert.AreEqual("CONTINUOUS_DRIVING_BREAK", dto.RuleAnalysis.RuleCode);
        Assert.AreEqual(270, dto.RuleAnalysis.DrivingLimitMinutes);
        Assert.AreEqual(45, dto.RuleAnalysis.RequiredBreakMinutes);
        Assert.AreEqual(292, dto.RuleAnalysis.ContinuousDrivingMinutes);
        Assert.AreEqual(22, dto.RuleAnalysis.ExceededMinutes);
        Assert.IsTrue(dto.RuleAnalysis.IsEstimated);
        Assert.IsTrue(dto.RuleAnalysis.BusinessSummary.Contains("4 godz. 52 min", StringComparison.Ordinal));
        Assert.IsTrue(dto.RuleAnalysis.BusinessSummary.Contains("22 min", StringComparison.Ordinal));
        Assert.AreEqual(3, dto.RuleAnalysis.Segments.Count);
        Assert.AreEqual(120, dto.RuleAnalysis.Segments[0].DrivingCounterAfterSegment);
        Assert.AreEqual(292, dto.RuleAnalysis.Segments[2].DrivingCounterAfterSegment);
    }

    [TestMethod]
    public void Map_DailyRestMetadata_RuleAnalysisIsNull()
    {
        var violation = CreateViolation(
            "DAILY_REST",
            """{"longestRestMinutes":405,"requiredRegularRestMinutes":660}""");

        var dto = Map(violation);

        Assert.IsNull(dto.RuleAnalysis);
    }


    private static ViolationDto Map(Violation violation, IReadOnlyList<DriverActivity>? activities = null)
    {
        var method = typeof(ViolationQueryService).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(x => x.Name == "Map" && x.GetParameters().Length == 2);

        return (ViolationDto)method.Invoke(null, [violation, activities ?? Array.Empty<DriverActivity>()])!;
    }


    private static DriverActivity Activity(
        Violation violation,
        string activityType,
        string startUtc,
        string endUtc)
    {
        return new DriverActivity
        {
            Id = Guid.NewGuid(),
            DddFileId = Guid.NewGuid(),
            DddFile = new DddFile
            {
                Id = Guid.NewGuid(),
                DriverId = violation.DriverId,
                CompanyId = violation.Driver?.CompanyId ?? Guid.NewGuid(),
                DriverCardNumber = violation.Driver?.CardNumber ?? string.Empty
            },
            ActivityType = activityType,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
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
