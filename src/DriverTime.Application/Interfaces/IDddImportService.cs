using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDddImportService
{
    Task<Guid> ImportAsync(
        string fileName,
        DddParseResultDto parsedData,
        CancellationToken cancellationToken = default);
}