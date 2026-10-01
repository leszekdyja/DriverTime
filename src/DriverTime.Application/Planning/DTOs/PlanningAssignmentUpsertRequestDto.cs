namespace DriverTime.Application.Planning.DTOs;

public class PlanningAssignmentUpsertRequestDto
{
    public DateOnly Date { get; set; }

    public Guid DriverId { get; set; }

    public Guid? PlanningDutyId { get; set; }

    public string AssignmentType { get; set; } = "Duty";

    public string? Notes { get; set; }
}
