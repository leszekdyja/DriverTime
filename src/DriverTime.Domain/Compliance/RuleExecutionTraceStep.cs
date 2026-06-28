namespace DriverTime.Domain.Compliance;

public class RuleExecutionTraceStep
{
    public int Order { get; set; }

    public DateTime? TimestampUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public long? CounterMinutes { get; set; }

    public bool IsResetPoint { get; set; }

    public bool IsViolationPoint { get; set; }

    public string Note { get; set; } = string.Empty;
}
