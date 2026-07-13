namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverDutyRuleDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }

    public string DriverFullName { get; set; } = string.Empty;

    public Guid DutyId { get; set; }

    public string DutyNumber { get; set; } = string.Empty;

    public string DutyName { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
