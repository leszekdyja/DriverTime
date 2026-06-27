namespace DriverTime.Application.Compliance;

public class ComplianceRecalculationResponseDto
{
    public int DriversCount { get; set; }

    public int RecalculatedDriversCount { get; set; }

    public int DeletedViolationsCount { get; set; }

    public int SavedViolationsCount { get; set; }

    public List<ComplianceDriverRecalculationResultDto> Drivers { get; set; } = new();
}
