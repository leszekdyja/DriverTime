using DriverTime.Application.Interfaces;

namespace DriverTime.Infrastructure.Services;

public class DddFileService : IDddFileService
{
    private readonly IDddParserGateway _parserGateway;
    private readonly IDddImportService _dddImportService;

    public DddFileService(
        IDddParserGateway parserGateway,
        IDddImportService dddImportService)
    {
        _parserGateway = parserGateway;
        _dddImportService = dddImportService;
    }

    public async Task<Guid> UploadAndParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.ddd");

        await using (var output = File.Create(tempFilePath))
        {
            await fileStream.CopyToAsync(output, cancellationToken);
        }

        var parsedData = await _parserGateway.ParseAsync(
            tempFilePath,
            cancellationToken);

        File.Delete(tempFilePath);

        return await _dddImportService.ImportAsync(
            fileName,
            parsedData,
            cancellationToken);
    }
}