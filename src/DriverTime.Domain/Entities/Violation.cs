using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class Violation : BaseEntity
{
    public Guid DriverId { get; set; }

    public Driver? Driver { get; set; }

    public string ViolationType { get; set; } = string.Empty;

    public string RegulationReference { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTime ViolationStart { get; set; }

    public DateTime ViolationEnd { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
