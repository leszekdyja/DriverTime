namespace DriverTime.Application.Dashboard.DTOs;

public class DriverRiskOverviewDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int LowRiskCount { get; set; }
    public int MediumRiskCount { get; set; }
    public int HighRiskCount { get; set; }
    public int CriticalRiskCount { get; set; }
    public List<DriverRiskDto> Drivers { get; set; } = new();
}

public class DriverRiskDto
{
    public Guid DriverId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CardNumber { get; set; } = string.Empty;
    public int ViolationsCount { get; set; }
    public int SevereViolationsCount { get; set; }
    public DateTime? LastImportAtUtc { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
    public int? DaysSinceLastImport { get; set; }
    public int? DaysSinceLastActivity { get; set; }
    public string RiskStatus { get; set; } = string.Empty;
    public int RiskScore { get; set; }
}
