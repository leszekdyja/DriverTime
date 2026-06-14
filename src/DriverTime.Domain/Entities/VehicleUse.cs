namespace DriverTime.Domain.Entities;

public class VehicleUse
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public DddFile DddFile { get; set; } = null!;

    public string RegistrationNumber { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }
}