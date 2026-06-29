namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportConfidenceDto
{
    public int DutyNumber { get; set; }

    public int StartTime { get; set; }

    public int EndTime { get; set; }

    public int Line { get; set; }

    public int Stops { get; set; }

    public int WorkingMinutes { get; set; }

    public int DrivingMinutes { get; set; }

    public int BreakMinutes { get; set; }

    public int DistanceKm { get; set; }
}
