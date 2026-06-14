namespace DriverTime.Application.DDD.DTOs;

public class DriverActivityDto
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }
}