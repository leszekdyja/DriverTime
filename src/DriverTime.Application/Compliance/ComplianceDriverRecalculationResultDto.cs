namespace DriverTime.Application.Compliance;

public class ComplianceDriverRecalculationResultDto
{
    public Guid DriverId { get; set; }

    public int DeletedViolationsCount { get; set; }

    public int SavedViolationsCount { get; set; }

    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;
}
