namespace DriverTime.Application.Dashboard.DTOs;

public class ComplianceRunDashboardStatsDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public int RecentRunsCount { get; set; }

    public string LastStatus { get; set; } = "NoData";

    public DateTime? LastRunAtUtc { get; set; }

    public int LastRunViolationsCount { get; set; }

    public int HighViolationsCount { get; set; }

    public int MediumViolationsCount { get; set; }

    public int LowViolationsCount { get; set; }

    public int DriversInLastRunCount { get; set; }

    public bool SchedulerEnabled { get; set; }

    public DateTime? LastSchedulerRunAtUtc { get; set; }

    public string LastSchedulerStatus { get; set; } = "NoData";

    public int LastSchedulerViolationsCount { get; set; }
}
