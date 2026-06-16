using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDddFileService
{
    Task<DddParseResultDto> UploadAndParseAsync(
        Stream fileStream,
        string originalFileName);

    Task<bool> RetryImportAsync(
        Guid monitoringId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DddFileDto>> GetAllAsync();

    Task<DddFileDetailsDto?> GetByIdAsync(Guid id);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
