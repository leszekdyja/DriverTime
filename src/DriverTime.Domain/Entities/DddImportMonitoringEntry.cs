namespace DriverTime.Domain.Entities;

public class DddImportMonitoringEntry
{
    public Guid Id { get; set; }

    public Guid? CompanyId { get; set; }

    public Company? Company { get; set; }

    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DddImportMonitoringStatus Status { get; set; } = DddImportMonitoringStatus.Pending;

    public string ErrorMessage { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    public DateTime? LastRetryAtUtc { get; set; }

    public string LastError { get; set; } = string.Empty;

    public string StoredFilePath { get; set; } = string.Empty;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
