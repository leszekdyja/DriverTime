namespace DriverTime.Domain.Entities;

public class VehicleUse
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public DddFile DddFile { get; set; } = null!;

    public string? VehicleRegistrationNumber { get; set; }

    public DateTime? StartTimeUtc { get; set; }

    public DateTime? EndTimeUtc { get; set; }
}