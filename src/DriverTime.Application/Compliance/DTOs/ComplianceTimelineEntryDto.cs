namespace DriverTime.Application.Compliance.DTOs;

public class ComplianceTimelineEntryDto
{
    public Guid SourceActivityId { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public long DurationMinutes { get; set; }
}
