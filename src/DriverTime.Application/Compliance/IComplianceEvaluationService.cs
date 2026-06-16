namespace DriverTime.Application.Compliance;

public interface IComplianceEvaluationService
{
    Task<int> EvaluateForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default);
}
