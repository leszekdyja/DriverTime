namespace DriverTime.Application.Planning.DTOs;

public class PlanningAssignmentRuleDto
{
    public Guid? CompanyId { get; set; }

    public Guid DriverId { get; set; }

    public Guid? DutyId { get; set; }

    public string? DutyNumber { get; set; }

    public string Type { get; set; } = "Forbidden";

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public string? Note { get; set; }
}
