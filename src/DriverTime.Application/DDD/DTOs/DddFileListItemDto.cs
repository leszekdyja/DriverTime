namespace DriverTime.Application.DTOs;

public class DddFileListItemDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public int DriverActivitiesCount { get; set; }

    public int VehicleUsesCount { get; set; }

    public int CountryEntriesCount { get; set; }
}