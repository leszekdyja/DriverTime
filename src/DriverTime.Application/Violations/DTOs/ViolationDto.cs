namespace DriverTime.Application.Violations.DTOs;

public class ViolationDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string ViolationType { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;

    public DateTime DetectedAtUtc { get; set; }

    public long ActualDurationMinutes { get; set; }

    public long LimitDurationMinutes { get; set; }

    public string MetadataJson { get; set; } = string.Empty;
}
