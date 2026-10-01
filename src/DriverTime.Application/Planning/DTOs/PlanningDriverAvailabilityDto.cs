namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverAvailabilityDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }

    public string DriverFullName { get; set; } = string.Empty;

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public string Type { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
