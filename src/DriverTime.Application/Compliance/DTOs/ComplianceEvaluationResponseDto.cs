namespace DriverTime.Application.Compliance.DTOs;

public class ComplianceEvaluationResponseDto
{
    public Guid DriverId { get; set; }

    public int SavedViolationsCount { get; set; }
}
