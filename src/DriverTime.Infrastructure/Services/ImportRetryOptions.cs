namespace DriverTime.Infrastructure.Services;

public class ImportRetryOptions
{
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;

    public int MaxRetryCount { get; set; } = 3;
}
