using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDriverDutyRule : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public Guid PlanningDutyId { get; set; }

    public PlanningDuty PlanningDuty { get; set; } = null!;

    public PlanningDriverDutyRuleType Type { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
