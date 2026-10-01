namespace DriverTime.Application.Planning.DTOs;

public class PlanningScheduleValidationDto
{
    public Guid ScheduleId { get; set; }

    public int WarningCount { get; set; }

    public int ErrorCount { get; set; }

    public List<PlanningScheduleValidationWarningDto> Warnings { get; set; } = new();
}
