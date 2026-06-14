namespace DriverTime.Application.DDD.DTOs;

public class DddFileDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }
}