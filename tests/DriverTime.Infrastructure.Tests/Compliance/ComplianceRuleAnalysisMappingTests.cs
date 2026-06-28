using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class ComplianceRuleAnalysisMappingTests
{
    [TestMethod]
    public void BuildRuleAnalysis_WithNativeDailyRestTrace_ReturnsNonEstimatedRuleAnalysis()
    {
        var violation = new ComplianceViolationCandidate
        {
            Code = "DAILY_REST",
            RuleName = "Daily rest",
            Severity = "HIGH",
            PeriodStartUtc = Utc("2026-06-17T00:00:00Z"),
            PeriodEndUtc = Utc("2026-06-18T00:30:00Z"),
            ActualMinutes = 300,
            LimitMinutes = 660,
            Metadata = new Dictionary<string, object>
            {
                ["requiredRestMinutes"] = 540L,
                ["longestRestMinutes"] = 300L,
                ["missingRestMinutes"] = 240L
            },
            ExecutionTrace = Trace("DAILY_REST", "Odpoczynek dzienny", isEstimated: false)
        };

        var analysis = ComplianceEngineService.BuildRuleAnalysis(violation);

        Assert.IsNotNull(analysis);
        Assert.AreEqual("DAILY_REST", analysis.RuleCode);
        Assert.AreEqual("Odpoczynek dzienny", analysis.RuleName);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.AreEqual(540, analysis.RequiredRestMinutes);
        Assert.AreEqual(300, analysis.LongestRestMinutes);
        Assert.AreEqual(240, analysis.MissingRestMinutes);
        Assert.IsNotNull(analysis.ExecutionTrace);
        Assert.IsTrue(analysis.Steps.Any(x => x.DetectsViolation));
        Assert.IsTrue(analysis.Segments.Any(x => x.ActivityType == "REST_GAP" && x.CountsAsRest));
    }

    [TestMethod]
    public void BuildRuleAnalysis_WithNativeContinuousDrivingTrace_ReturnsNonEstimatedRuleAnalysis()
    {
        var violation = new ComplianceViolationCandidate
        {
            Code = "CONTINUOUS_DRIVING_BREAK",
            RuleName = "Continuous driving break",
            Severity = "HIGH",
            PeriodStartUtc = Utc("2026-06-17T08:00:00Z"),
            PeriodEndUtc = Utc("2026-06-17T12:40:00Z"),
            ActualMinutes = 280,
            LimitMinutes = 270,
            Metadata = new Dictionary<string, object>
            {
                ["limitMinutes"] = 270L,
                ["requiredBreakMinutes"] = 45L,
                ["continuousDrivingMinutes"] = 280L,
                ["exceededMinutes"] = 10L
            },
            ExecutionTrace = Trace("CONTINUOUS_DRIVING_BREAK", "Przerwa po 4 godz. 30 min jazdy", isEstimated: false)
        };

        var analysis = ComplianceEngineService.BuildRuleAnalysis(violation);

        Assert.IsNotNull(analysis);
        Assert.AreEqual("CONTINUOUS_DRIVING_BREAK", analysis.RuleCode);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.AreEqual(270, analysis.DrivingLimitMinutes);
        Assert.AreEqual(45, analysis.RequiredBreakMinutes);
        Assert.AreEqual(280, analysis.ContinuousDrivingMinutes);
        Assert.AreEqual(10, analysis.ExceededMinutes);
        Assert.IsTrue(analysis.Steps.Any(x => x.DetectsViolation));
    }

    [TestMethod]
    public void BuildRuleAnalysis_WithNativeDailyDrivingTrace_ReturnsNonEstimatedRuleAnalysis()
    {
        var violation = new ComplianceViolationCandidate
        {
            Code = "DAILY_DRIVING_LIMIT",
            RuleName = "Daily driving limit",
            Severity = "HIGH",
            PeriodStartUtc = Utc("2026-06-08T08:00:00Z"),
            PeriodEndUtc = Utc("2026-06-08T18:31:00Z"),
            ActualMinutes = 601,
            LimitMinutes = 600,
            Metadata = new Dictionary<string, object>
            {
                ["limitMinutes"] = 600L,
                ["totalDrivingMinutes"] = 601L,
                ["exceededMinutes"] = 1L
            },
            ExecutionTrace = Trace("DAILY_DRIVING_LIMIT", "Limit jazdy dziennej", isEstimated: false)
        };

        var analysis = ComplianceEngineService.BuildRuleAnalysis(violation);

        Assert.IsNotNull(analysis);
        Assert.AreEqual("DAILY_DRIVING_LIMIT", analysis.RuleCode);
        Assert.AreEqual("Limit jazdy dziennej", analysis.RuleName);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.AreEqual(600, analysis.DrivingLimitMinutes);
        Assert.AreEqual(601, analysis.ContinuousDrivingMinutes);
        Assert.AreEqual(1, analysis.ExceededMinutes);
        Assert.IsTrue(analysis.Steps.Any(x => x.DetectsViolation));
    }

    [TestMethod]
    public void BuildRuleAnalysis_WithNativeWeeklyDrivingTrace_ReturnsNonEstimatedRuleAnalysis()
    {
        var violation = new ComplianceViolationCandidate
        {
            Code = "WEEKLY_DRIVING_LIMIT",
            RuleName = "Weekly driving limit",
            Severity = "HIGH",
            PeriodStartUtc = Utc("2026-06-08T00:00:00Z"),
            PeriodEndUtc = Utc("2026-06-15T00:00:00Z"),
            ActualMinutes = 3420,
            LimitMinutes = 3360,
            Metadata = new Dictionary<string, object>
            {
                ["limitMinutes"] = 3360L,
                ["totalDrivingMinutes"] = 3420L,
                ["exceededMinutes"] = 60L
            },
            ExecutionTrace = Trace("WEEKLY_DRIVING_LIMIT", "Limit jazdy tygodniowej", isEstimated: false)
        };

        var analysis = ComplianceEngineService.BuildRuleAnalysis(violation);

        Assert.IsNotNull(analysis);
        Assert.AreEqual("WEEKLY_DRIVING_LIMIT", analysis.RuleCode);
        Assert.AreEqual("Limit jazdy tygodniowej", analysis.RuleName);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.AreEqual(3360, analysis.DrivingLimitMinutes);
        Assert.AreEqual(3420, analysis.ContinuousDrivingMinutes);
        Assert.AreEqual(60, analysis.ExceededMinutes);
        Assert.IsTrue(analysis.Steps.Any(x => x.DetectsViolation));
    }

    [TestMethod]
    public void BuildRuleAnalysis_WithNativeBiWeeklyDrivingTrace_ReturnsNonEstimatedRuleAnalysis()
    {
        var violation = new ComplianceViolationCandidate
        {
            Code = "BI_WEEKLY_DRIVING_LIMIT",
            RuleName = "Bi-weekly driving limit",
            Severity = "HIGH",
            PeriodStartUtc = Utc("2026-06-08T00:00:00Z"),
            PeriodEndUtc = Utc("2026-06-22T00:00:00Z"),
            ActualMinutes = 5460,
            LimitMinutes = 5400,
            Metadata = new Dictionary<string, object>
            {
                ["limitMinutes"] = 5400L,
                ["totalDrivingMinutes"] = 5460L,
                ["exceededMinutes"] = 60L,
                ["firstWeekDrivingMinutes"] = 2880L,
                ["secondWeekDrivingMinutes"] = 2580L
            },
            ExecutionTrace = Trace("BI_WEEKLY_DRIVING_LIMIT", "Limit jazdy w dw?ch kolejnych tygodniach", isEstimated: false)
        };
        violation.ExecutionTrace.Metrics["Pierwszy tydzie?"] = "48 godz. 0 min";
        violation.ExecutionTrace.Metrics["Drugi tydzie?"] = "43 godz. 0 min";

        var analysis = ComplianceEngineService.BuildRuleAnalysis(violation);

        Assert.IsNotNull(analysis);
        Assert.AreEqual("BI_WEEKLY_DRIVING_LIMIT", analysis.RuleCode);
        Assert.AreEqual("Limit jazdy w dw?ch kolejnych tygodniach", analysis.RuleName);
        Assert.IsFalse(analysis.IsEstimated);
        Assert.AreEqual(5400, analysis.DrivingLimitMinutes);
        Assert.AreEqual(5460, analysis.ContinuousDrivingMinutes);
        Assert.AreEqual(60, analysis.ExceededMinutes);
        Assert.IsNotNull(analysis.ExecutionTrace);
        Assert.IsTrue(analysis.ExecutionTrace.Metrics.ContainsKey("Pierwszy tydzie?"));
        Assert.IsTrue(analysis.ExecutionTrace.Metrics.ContainsKey("Drugi tydzie?"));
        Assert.IsTrue(analysis.Steps.Any(x => x.DetectsViolation));
    }

    [TestMethod]
    public void BuildRuleAnalysis_WithoutNativeTrace_ReturnsNullForPreviewMapping()
    {
        var violation = new ComplianceViolationCandidate
        {
            Code = "DAILY_REST",
            RuleName = "Daily rest",
            PeriodStartUtc = Utc("2026-06-17T00:00:00Z"),
            PeriodEndUtc = Utc("2026-06-18T00:00:00Z"),
            Metadata = new Dictionary<string, object>()
        };

        var analysis = ComplianceEngineService.BuildRuleAnalysis(violation);

        Assert.IsNull(analysis);
    }

    private static RuleExecutionTrace Trace(string ruleCode, string ruleName, bool isEstimated)
    {
        return new RuleExecutionTrace
        {
            RuleCode = ruleCode,
            RuleName = ruleName,
            AnalysisWindowStartUtc = Utc("2026-06-17T00:00:00Z"),
            AnalysisWindowEndUtc = Utc("2026-06-18T00:00:00Z"),
            DetectedAtUtc = Utc("2026-06-18T00:30:00Z"),
            IsEstimated = isEstimated,
            Summary = "Podsumowanie po polsku z natywnego trace.",
            Metrics = new Dictionary<string, string>
            {
                ["Analiza szacunkowa"] = isEstimated ? "Tak" : "Nie"
            },
            Steps =
            [
                new RuleExecutionTraceStep
                {
                    Order = 1,
                    TimestampUtc = Utc("2026-06-17T00:00:00Z"),
                    Description = "Start analizy.",
                    CounterMinutes = 0
                },
                new RuleExecutionTraceStep
                {
                    Order = 2,
                    TimestampUtc = Utc("2026-06-18T00:30:00Z"),
                    Description = "Wykryto naruszenie.",
                    CounterMinutes = 280,
                    IsViolationPoint = true
                }
            ],
            Segments =
            [
                new RuleExecutionTraceSegment
                {
                    StartUtc = Utc("2026-06-17T01:00:00Z"),
                    EndUtc = Utc("2026-06-17T06:00:00Z"),
                    ActivityType = "REST_GAP",
                    DurationMinutes = 300,
                    RestCandidateMinutes = 300,
                    Note = "Luka jako odpoczynek."
                }
            ]
        };
    }

    private static DateTime Utc(string value) =>
        DateTime.Parse(value).ToUniversalTime();
}
