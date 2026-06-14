namespace DriverTime.Domain.Entities;

public class DriverActivity
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public DddFile DddFile { get; set; } = null!;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;
}