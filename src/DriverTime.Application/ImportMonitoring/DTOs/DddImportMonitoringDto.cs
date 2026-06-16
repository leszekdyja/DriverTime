namespace DriverTime.Application.ImportMonitoring.DTOs;

public class DddImportMonitoringDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public DateTime? LastRetryAtUtc { get; set; }

    public string LastError { get; set; } = string.Empty;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? UserId { get; set; }
}
