namespace DriverTime.Application.Violations.DTOs;

public class ViolationDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string ViolationType { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;

    public DateTime DetectedAtUtc { get; set; }

    public long ActualDurationMinutes { get; set; }

    public long LimitDurationMinutes { get; set; }

    public string MetadataJson { get; set; } = string.Empty;

    public long? ActualValueMinutes { get; set; }

    public long? RequiredValueMinutes { get; set; }

    public long? DifferenceMinutes { get; set; }

    public long? MissingMinutes { get; set; }

    public long? ExcessMinutes { get; set; }

    public long? CompensationMinutes { get; set; }

    public DateTime? CompensationDeadlineUtc { get; set; }

    public string BusinessSummary { get; set; } = string.Empty;

    public string ScaleLabel { get; set; } = string.Empty;

    public DispatcherRecommendationDto? DispatcherRecommendation { get; set; }

    public ViolationBusinessDetailsDto? BusinessDetails { get; set; }

    public ViolationRuleAnalysisDto? RuleAnalysis { get; set; }
}

public class ViolationBusinessDetailsDto
{
    public long? ActualRestMinutes { get; set; }

    public long? RequiredRestMinutes { get; set; }

    public long? MissingRestMinutes { get; set; }

    public long? ReducedWeeklyRestMinutes { get; set; }

    public long? CompensationDebtMinutes { get; set; }

    public DateTime? CompensationDeadlineUtc { get; set; }

    public string CountryIssueMessage { get; set; } = string.Empty;

    public long? ContinuousDrivingMinutes { get; set; }

    public long? RequiredBreakMinutes { get; set; }

    public long? ReceivedBreakMinutes { get; set; }

    public long? DrivingLimitMinutes { get; set; }

    public long? DrivingExceededMinutes { get; set; }

    public string BreakType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;
}

public class DispatcherRecommendationDto
{
    public string Status { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<string> RecommendedActions { get; set; } = new();

    public bool CanDrive { get; set; }

    public bool CanStartShift { get; set; }

    public bool PlannerAttentionRequired { get; set; }

    public DateTimeOffset? EarliestNextDriveUtc { get; set; }
}


public class ViolationRuleAnalysisDto
{
    public string RuleName { get; set; } = string.Empty;

    public string RuleCode { get; set; } = string.Empty;

    public long DrivingLimitMinutes { get; set; }

    public long RequiredBreakMinutes { get; set; }

    public DateTime ViolationDetectedAtUtc { get; set; }

    public long ContinuousDrivingMinutes { get; set; }

    public long ExceededMinutes { get; set; }

    public DateTime? AnalysisWindowStartUtc { get; set; }

    public DateTime? AnalysisWindowEndUtc { get; set; }

    public long? RequiredRestMinutes { get; set; }

    public long? LongestRestMinutes { get; set; }

    public long? MissingRestMinutes { get; set; }

    public bool IsEstimated { get; set; }

    public string BusinessSummary { get; set; } = string.Empty;

    public RuleExecutionTraceDto? ExecutionTrace { get; set; }

    public List<RuleExecutionTraceStepDto> Steps { get; set; } = new();

    public List<ViolationRuleAnalysisSegmentDto> Segments { get; set; } = new();
}

public class RuleExecutionTraceDto
{
    public string RuleCode { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public DateTime? AnalysisWindowStartUtc { get; set; }

    public DateTime? AnalysisWindowEndUtc { get; set; }

    public DateTime? DetectedAtUtc { get; set; }

    public bool IsEstimated { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Dictionary<string, string> Metrics { get; set; } = new();

    public List<RuleExecutionTraceStepDto> Steps { get; set; } = new();

    public List<RuleExecutionTraceSegmentDto> Segments { get; set; } = new();
}

public class RuleExecutionTraceStepDto
{
    public int Order { get; set; }

    public DateTime? TimeUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public long? CounterMinutes { get; set; }

    public bool ResetsCounter { get; set; }

    public bool DetectsViolation { get; set; }
}

public class RuleExecutionTraceSegmentDto
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

public class ViolationRuleAnalysisSegmentDto
{
    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public long DurationMinutes { get; set; }

    public bool IncreasesDrivingCounter { get; set; }

    public bool ResetsCounter { get; set; }

    public long DrivingCounterAfterSegment { get; set; }

    public bool CountsAsRest { get; set; }

    public long? RestCandidateMinutes { get; set; }
}
