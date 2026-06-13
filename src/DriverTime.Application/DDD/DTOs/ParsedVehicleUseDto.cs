namespace DriverTime.Application.DDD.DTOs;

public class ParsedVehicleUseDto
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
