namespace DriverTime.Application.Planning.DTOs;

public class PlanningScheduleValidationWarningDto
{
    public string Severity { get; set; } = string.Empty;

    public DateOnly? Date { get; set; }

    public Guid? DriverId { get; set; }

    public string? DriverName { get; set; }

    public Guid? AssignmentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
