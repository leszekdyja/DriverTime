namespace DriverTime.Application.Interfaces;

public interface IViolationDetectionService
{
    Task<int> DetectForDriverAsync(
        Guid companyId,
        Guid driverId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    Task<int> DetectAfterImportAsync(
        Guid importId,
        CancellationToken cancellationToken = default);
}
