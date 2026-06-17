namespace DriverTime.Application.Downloads.DTOs;

public class DriverDownloadDto
{
    public Guid DriverId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string CardNumber { get; set; } = string.Empty;

    public DateTime? LastDownloadUtc { get; set; }

    public DateTime? NextRequiredDownloadUtc { get; set; }

    public int? DaysUntilDue { get; set; }

    public string Status { get; set; } = DownloadStatus.Overdue;
}
