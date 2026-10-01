namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverDutyRuleRequestDto
{
    public Guid DriverId { get; set; }

    public Guid DutyId { get; set; }

    public string Type { get; set; } = "Forbidden";

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string? Notes { get; set; }
}
