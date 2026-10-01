namespace DriverTime.Infrastructure.Compliance;

public class ComplianceSchedulerOptions
{
    public bool Enabled { get; set; } = false;

    public int IntervalMinutes { get; set; } = 60;

    public int MaxDriversPerRun { get; set; } = 100;
}
