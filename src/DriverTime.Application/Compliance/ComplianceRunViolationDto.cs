namespace DriverTime.Application.Compliance;

public class ComplianceRunViolationDto
{
    public Guid Id { get; set; }

    public Guid ComplianceRunId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime PeriodStartUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public int ActualMinutes { get; set; }

    public int LimitMinutes { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }
}
