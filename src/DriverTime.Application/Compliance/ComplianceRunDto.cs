namespace DriverTime.Application.Compliance;

public class ComplianceRunDto
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid DriverId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public int TimelineCount { get; set; }

    public int ViolationsCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<ComplianceRunViolationDto> Violations { get; set; } = new();
}
