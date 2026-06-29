namespace DriverTime.Application.Planning.DTOs;

public class PlanningAssignmentDto
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public Guid DriverId { get; set; }

    public string DriverFullName { get; set; } = string.Empty;

    public Guid? PlanningDutyId { get; set; }

    public string? DutyNumber { get; set; }

    public string? Line { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string AssignmentType { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
