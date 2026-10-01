namespace DriverTime.Application.Planning.DTOs;

public class PlanningScheduleCreateRequestDto
{
    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public string? Notes { get; set; }
}
