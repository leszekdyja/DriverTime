namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyDetailsDto : PlanningDutyListDto
{
    public string? Notes { get; set; }

    public string? SourceFileName { get; set; }

    public List<PlanningDutyLineDto> Lines { get; set; } = new();

    public List<PlanningDutyStopDto> Stops { get; set; } = new();
}
