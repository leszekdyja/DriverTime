namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyStopDto
{
    public Guid Id { get; set; }

    public int Sequence { get; set; }

    public string StopName { get; set; } = string.Empty;

    public decimal? Km { get; set; }

    public string? TripGroup { get; set; }

    public TimeOnly? ArrivalTime { get; set; }

    public TimeOnly? DepartureTime { get; set; }

    public string? LineCode { get; set; }
}
