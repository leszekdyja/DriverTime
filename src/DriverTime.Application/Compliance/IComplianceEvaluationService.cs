namespace DriverTime.Application.Compliance;

public interface IComplianceEvaluationService
{
    Task<int> EvaluateForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default);

    Task<ComplianceDriverRecalculationResultDto?> RecalculateForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default);

    Task<ComplianceRecalculationResponseDto> RecalculateForCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);
}
