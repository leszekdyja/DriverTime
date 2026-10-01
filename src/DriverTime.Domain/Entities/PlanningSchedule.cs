using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningSchedule : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedUtc { get; set; }

    public ICollection<PlanningAssignment> Assignments { get; set; } = new List<PlanningAssignment>();
}
