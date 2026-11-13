namespace DriverTime.Domain.Entities;

public class DriverActivity
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public DddFile DddFile { get; set; } = null!;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    // Null for older imports and manual entries: neither proves crew status.
    public bool? IsCrew { get; set; }

    // 1 = driver, 2 = co-driver; null when the card was not inserted.
    public int? CardSlot { get; set; }
}
