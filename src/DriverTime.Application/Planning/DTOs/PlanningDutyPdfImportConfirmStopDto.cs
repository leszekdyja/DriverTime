namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportConfirmStopDto
{
    public string? StopName { get; set; }

    public TimeOnly? ArrivalTime { get; set; }

    public TimeOnly? DepartureTime { get; set; }

    public int Sequence { get; set; }
}
