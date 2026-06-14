using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;

namespace DriverTime.Infrastructure.Services;

public class DddImportService : IDddImportService
{
    private readonly ApplicationDbContext _dbContext;

    public DddImportService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> ImportAsync(
        string fileName,
        DddParseResultDto parsedData,
        CancellationToken cancellationToken = default)
    {
        var dddFile = new DddFile
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.DddFiles.Add(dddFile);

        foreach (var activity in parsedData.Activities)
        {
            if (!DateTime.TryParse(activity.Start, out var startUtc))
            {
                continue;
            }

            if (!DateTime.TryParse(activity.End, out var endUtc))
            {
                continue;
            }

            var driverActivity = new DriverActivity
            {
                Id = Guid.NewGuid(),
                DddFileId = dddFile.Id,
                StartUtc = startUtc,
                EndUtc = endUtc,
                ActivityType = activity.Activity
            };

            _dbContext.DriverActivities.Add(driverActivity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return dddFile.Id;
    }
}