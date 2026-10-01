namespace DriverTime.Application.Planning;

public record PlanningAssignmentRule
{
    public Guid CompanyId { get; init; }

    public Guid DriverId { get; init; }

    public Guid? DutyId { get; init; }

    public string? DutyNumber { get; init; }

    public PlanningAssignmentRuleType Type { get; init; }

    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    public string? Note { get; init; }
}
