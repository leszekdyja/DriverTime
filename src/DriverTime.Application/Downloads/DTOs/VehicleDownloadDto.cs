namespace DriverTime.Application.Downloads.DTOs;

public class VehicleDownloadDto
{
    public Guid VehicleId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public DateTime? LastDownloadUtc { get; set; }

    public DateTime? NextRequiredDownloadUtc { get; set; }

    public int? DaysUntilDue { get; set; }

    public string Status { get; set; } = DownloadStatus.Overdue;
}
