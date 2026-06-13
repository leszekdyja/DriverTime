using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;

namespace DriverTime.Infrastructure.Services;

public class DddFileService : IDddFileService
{
    private readonly ApplicationDbContext _dbContext;

    public DddFileService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> UploadAndParseAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var dddFile = new DddFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.DddFiles.Add(dddFile);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return dddFile.Id;
    }
}