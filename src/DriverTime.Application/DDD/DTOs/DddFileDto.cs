namespace DriverTime.Application.DDD.DTOs;

public class DddFileDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string DriverStatus { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public int ActivitiesCount { get; set; }
}
