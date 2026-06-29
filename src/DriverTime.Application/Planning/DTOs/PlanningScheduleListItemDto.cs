namespace DriverTime.Application.Planning.DTOs;

public class PlanningScheduleListItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    public int AssignmentsCount { get; set; }
}
