namespace DriverTime.Application.Compliance.DTOs;

public class ComplianceViolationPreviewDto
{
    public string Code { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime PeriodStartUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public long ActualMinutes { get; set; }

    public long LimitMinutes { get; set; }
}
