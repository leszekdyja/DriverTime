using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDddFileService
{
    Task<DddParseResultDto> UploadAndParseAsync(
        Stream fileStream,
        string originalFileName);

    Task<IReadOnlyList<DddFileDto>> GetAllAsync();

    Task<DddFileDetailsDto?> GetByIdAsync(Guid id);
}