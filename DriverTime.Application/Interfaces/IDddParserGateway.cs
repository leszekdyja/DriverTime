using DriverTime.Application.DTOs.Ddd;

namespace DriverTime.Application.Interfaces;

public interface IDddParserGateway
{
    Task<DddParseResultDto> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
