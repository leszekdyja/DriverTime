namespace DriverTime.Application.Planning.DTOs;

public class PlanningScheduleDto : PlanningScheduleListItemDto
{
    public List<PlanningAssignmentDto> Assignments { get; set; } = new();
}
