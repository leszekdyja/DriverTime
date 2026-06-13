namespace DriverTime.Application.Interfaces;

public interface IDddFileService
{
    Task<Guid> UploadAndParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default);
}