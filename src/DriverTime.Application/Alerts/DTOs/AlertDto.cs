namespace DriverTime.Application.Alerts.DTOs;

public class AlertDto
{
    public string Id { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RelatedEntityType { get; set; } = string.Empty;

    public Guid? RelatedEntityId { get; set; }

    public string RelatedEntityName { get; set; } = string.Empty;

    public DateTime? DueDateUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string Status { get; set; } = "Open";

    public string ActionUrl { get; set; } = string.Empty;
}
