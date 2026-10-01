namespace DriverTime.Application.Compliance.DTOs;

public class ComplianceDebugSummaryDto
{
    public Dictionary<string, long> DrivingMinutesByDay { get; set; } = new();

    public Dictionary<string, long> RestMinutesByDay { get; set; } = new();

    public Dictionary<string, long> WeeklyDrivingMinutes { get; set; } = new();

    public Dictionary<string, long> BiWeeklyDrivingMinutes { get; set; } = new();

    public List<string> RegisteredRuleCodes { get; set; } = new();
}
