using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DddFileService : IDddFileService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly IDddParserGateway _dddParserGateway;

    public DddFileService(
        DriverTimeDbContext dbContext,
        IDddParserGateway dddParserGateway)
    {
        _dbContext = dbContext;
        _dddParserGateway = dddParserGateway;
    }

    public async Task<DddParseResultDto> UploadAndParseAsync(
        Stream fileStream,
        string originalFileName)
    {
        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}_{originalFileName}");

        await using (var output = File.Create(tempFilePath))
        {
            await fileStream.CopyToAsync(output);
        }

        try
        {
            var parseResult = await _dddParserGateway.ParseAsync(tempFilePath);

            var dddFile = new DddFile
            {
                Id = Guid.NewGuid(),
                FileName = originalFileName,
                UploadedAtUtc = DateTime.UtcNow
            };

            _dbContext.DddFiles.Add(dddFile);

            await _dbContext.SaveChangesAsync();

            return parseResult;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public async Task<IReadOnlyList<DddFileDto>> GetAllAsync()
    {
        return await _dbContext.DddFiles
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new DddFileDto
            {
                Id = x.Id,
                FileName = x.FileName,
                DriverFirstName = x.DriverFirstName,
                DriverLastName = x.DriverLastName,
                DriverCardNumber = x.DriverCardNumber,
                UploadedAtUtc = x.UploadedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DddFileDetailsDto?> GetByIdAsync(Guid id)
    {
        return await _dbContext.DddFiles
            .Where(x => x.Id == id)
            .Select(x => new DddFileDetailsDto
            {
                Id = x.Id,
                FileName = x.FileName
            })
            .FirstOrDefaultAsync();
    }
}
