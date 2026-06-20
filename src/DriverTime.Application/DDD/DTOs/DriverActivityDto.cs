namespace DriverTime.Application.DDD.DTOs;

public class DriverActivityDto
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string VehicleRegistration { get; set; } = string.Empty;

    public string VehicleRegistrationNumber { get; set; } = string.Empty;

    public string Vehicle { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }
}
