using DriverTime.Application.DDD;
using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDddParserGateway
{
    Task<DddParseResultDto> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}