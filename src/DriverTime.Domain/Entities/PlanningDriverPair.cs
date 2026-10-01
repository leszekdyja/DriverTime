using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDriverPair : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid FirstDriverId { get; set; }
    public Driver FirstDriver { get; set; } = null!;
    public Guid SecondDriverId { get; set; }
    public Driver SecondDriver { get; set; } = null!;
    public bool IsNightDutyPair { get; set; }
    public bool PreventSameShift { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
