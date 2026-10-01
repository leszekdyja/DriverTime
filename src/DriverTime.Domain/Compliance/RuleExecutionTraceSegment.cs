namespace DriverTime.Domain.Compliance;

public class RuleExecutionTraceSegment
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public long DurationMinutes { get; set; }

    public long DrivingMinutesAfterSegment { get; set; }

    public long? RestCandidateMinutes { get; set; }

    public bool IsResetPoint { get; set; }

    public bool IsViolationPoint { get; set; }

    public string Note { get; set; } = string.Empty;
}
