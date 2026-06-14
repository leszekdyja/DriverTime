using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDddFileService
{
    Task<DddParseResultDto> UploadAndParseAsync(
        Stream fileStream,
        string fileName);

    Task<IEnumerable<DddFileDto>> GetAllAsync();

    Task<DddFileDetailsDto?> GetByIdAsync(Guid id);
}