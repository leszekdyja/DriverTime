using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningAssignment : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Guid PlanningScheduleId { get; set; }

    public PlanningSchedule PlanningSchedule { get; set; } = null!;

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public Guid? PlanningDutyId { get; set; }

    public PlanningDuty? PlanningDuty { get; set; }

    public DateOnly Date { get; set; }

    public PlanningAssignmentType AssignmentType { get; set; } = PlanningAssignmentType.Duty;

    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedUtc { get; set; }
}
