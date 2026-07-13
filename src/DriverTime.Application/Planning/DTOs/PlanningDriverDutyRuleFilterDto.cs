namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverDutyRuleFilterDto
{
    public Guid? DriverId { get; set; }

    public Guid? DutyId { get; set; }

    public string? Type { get; set; }

    public DateOnly? ActiveOn { get; set; }
}
