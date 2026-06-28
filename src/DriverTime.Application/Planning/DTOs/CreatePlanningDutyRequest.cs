namespace DriverTime.Application.Planning.DTOs;

public class CreatePlanningDutyRequest
{
    public string DutyNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }

    public string? VehicleRequirement { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int? TotalDurationMinutes { get; set; }

    public int? WorkMinutes { get; set; }

    public int? BreakMinutes { get; set; }

    public int? DrivingMinutes { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Notes { get; set; }

    public string? SourceFileName { get; set; }

    public List<PlanningDutyLineDto> Lines { get; set; } = new();

    public List<PlanningDutyStopDto> Stops { get; set; } = new();
}
