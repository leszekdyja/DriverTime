namespace DriverTime.Domain.Entities;

public class ComplianceRun
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public string Trigger { get; set; } = string.Empty;

    public int TimelineCount { get; set; }

    public int ViolationsCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ComplianceRunViolation> Violations { get; set; }
        = new List<ComplianceRunViolation>();
}
