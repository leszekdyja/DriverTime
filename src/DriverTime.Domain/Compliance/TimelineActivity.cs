namespace DriverTime.Domain.Compliance;

public class TimelineActivity
{
    public Guid SourceActivityId { get; set; }

    public Guid DriverId { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public TimeSpan Duration => EndUtc > StartUtc
        ? EndUtc - StartUtc
        : TimeSpan.Zero;
}
