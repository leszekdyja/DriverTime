namespace DriverTime.Application.Vehicles.DTOs;

public class VehicleDto
{
    public Guid Id { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public string Vin { get; set; } = string.Empty;

    public bool Active { get; set; }
}
