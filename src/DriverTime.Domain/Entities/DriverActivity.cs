using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class DriverActivity : BaseEntity
{
    public Guid DriverId { get; set; }

    public Driver? Driver { get; set; }

    public Guid? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string ActivityType { get; set; } = string.Empty;
}
