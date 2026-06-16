using DriverTime.Application.ImportMonitoring.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDddImportMonitoringService
{
    Task<DddImportMonitoringDto> CreateAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    Task MarkProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        Guid id,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task SetStoredFilePathAsync(
        Guid id,
        string storedFilePath,
        CancellationToken cancellationToken = default);

    Task MarkRetryProcessingAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DddImportMonitoringDto>> GetFailedRetryCandidatesAsync(
        int maxRetryCount,
        int take = 10,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DddImportMonitoringDto>> GetRecentAsync(
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<DddImportMonitoringDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
