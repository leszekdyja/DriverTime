namespace DriverTime.Domain.Compliance;

public class RuleExecutionTrace
{
    public string RuleCode { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public DateTime? AnalysisWindowStartUtc { get; set; }

    public DateTime? AnalysisWindowEndUtc { get; set; }

    public DateTime? DetectedAtUtc { get; set; }

    public bool IsEstimated { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Dictionary<string, string> Metrics { get; set; } = new();

    public List<RuleExecutionTraceStep> Steps { get; set; } = new();

    public List<RuleExecutionTraceSegment> Segments { get; set; } = new();
}
