namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyLineDto
{
    public Guid Id { get; set; }

    public string LineCode { get; set; } = string.Empty;

    public string? Variant { get; set; }

    public decimal? DistanceKm { get; set; }
}
