namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverAvailabilityCreateRequestDto
{
    public Guid DriverId { get; set; }

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public string Type { get; set; } = "Unavailable";

    public string? Note { get; set; }
}
