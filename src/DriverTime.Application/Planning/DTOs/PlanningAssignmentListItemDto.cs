namespace DriverTime.Application.Planning.DTOs;

public class PlanningAssignmentListItemDto
{
    public Guid Id { get; set; }

    public DateOnly WorkDate { get; set; }

    public Guid DriverId { get; set; }

    public string DriverFullName { get; set; } = string.Empty;

    public Guid? PlanningDutyId { get; set; }

    public string? DutyNumber { get; set; }

    public DateTime? StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public string Status { get; set; } = string.Empty;
}
