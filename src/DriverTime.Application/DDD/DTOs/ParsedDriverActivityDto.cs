namespace DriverTime.Application.DDD.DTOs;

public class ParsedDriverActivityDto
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public string Activity { get; set; } = string.Empty;

    public string ActivityCode { get; set; } = string.Empty;

    public string VehicleRegistration { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
