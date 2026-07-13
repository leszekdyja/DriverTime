namespace DriverTime.Application.Planning.DTOs;

public class PlanningManualAssignmentRequestDto
{
    public Guid DriverId { get; set; }

    public DateOnly Date { get; set; }

    public Guid? DutyId { get; set; }

    public string? EntryCode { get; set; }

    public string? Notes { get; set; }
}
