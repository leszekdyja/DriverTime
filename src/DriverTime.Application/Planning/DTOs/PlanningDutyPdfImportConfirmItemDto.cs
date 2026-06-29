namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportConfirmItemDto
{
    public string? DutyNumber { get; set; }

    public string? DutyName { get; set; }

    public string? Line { get; set; }

    public DateOnly? ValidFrom { get; set; }

    public string? VehicleRequirement { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int? WorkingMinutes { get; set; }

    public int? DrivingMinutes { get; set; }

    public int? BreakMinutes { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Notes { get; set; }

    public List<PlanningDutyPdfImportConfirmStopDto> Stops { get; set; } = new();
}

