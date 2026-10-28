namespace DriverTime.Domain.Compliance;

public class TimelineActivity
{
    public Guid SourceActivityId { get; set; }

    public Guid DriverId { get; set; }

    // End of the source download's coverage, not its upload time.
    public DateTime? SourceCoverageEndUtc { get; set; }

    public string ActivityType { get; set; } = string.Empty;

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public TimeSpan Duration => EndUtc > StartUtc
        ? EndUtc - StartUtc
        : TimeSpan.Zero;
}
