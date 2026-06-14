using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DddFileService : IDddFileService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDddParserGateway _parserGateway;

    public DddFileService(
        ApplicationDbContext dbContext,
        IDddParserGateway parserGateway)
    {
        _dbContext = dbContext;
        _parserGateway = parserGateway;
    }

    public async Task<DddParseResultDto> UploadAndParseAsync(
        Stream fileStream,
        string originalFileName)
    {
        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.ddd");

        await using (var file = File.Create(tempFilePath))
        {
            await fileStream.CopyToAsync(file);
        }

        var parseResult = await _parserGateway.ParseAsync(tempFilePath);

        var dddFile = new DddFile
        {
            Id = Guid.NewGuid(),
            FileName = originalFileName,
            UploadedAtUtc = DateTime.UtcNow
        };

        foreach (var activity in parseResult.Activities)
        {
            if (!DateTime.TryParse(activity.Start, out var startUtc))
            {
                continue;
            }

            if (!DateTime.TryParse(activity.End, out var endUtc))
            {
                continue;
            }

            startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
            endUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);

            dddFile.DriverActivities.Add(new DriverActivity
            {
                Id = Guid.NewGuid(),
                DddFileId = dddFile.Id,
                StartUtc = startUtc,
                EndUtc = endUtc,
                ActivityType = activity.Activity
            });
        }

        foreach (var countryEntry in parseResult.CountryCodeEntries)
        {
            if (!DateTime.TryParse(countryEntry.Timestamp, out var entryUtc))
            {
                continue;
            }

            entryUtc = DateTime.SpecifyKind(entryUtc, DateTimeKind.Utc);

            dddFile.CountryEntries.Add(new CountryEntry
            {
                Id = Guid.NewGuid(),
                DddFileId = dddFile.Id,
                EntryTimeUtc = entryUtc,
                CountryCode = countryEntry.CountryCode
            });
        }

        foreach (var vehicleUse in parseResult.VehicleUses)
        {
            if (!DateTime.TryParse(vehicleUse.Start, out var startUtc))
            {
                continue;
            }

            if (!DateTime.TryParse(vehicleUse.End, out var endUtc))
            {
                continue;
            }

            startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
            endUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);

            dddFile.VehicleUses.Add(new VehicleUse
            {
                Id = Guid.NewGuid(),
                DddFileId = dddFile.Id,
                RegistrationNumber = vehicleUse.VehicleRegistration,
                StartUtc = startUtc,
                EndUtc = endUtc
            });
        }

        _dbContext.DddFiles.Add(dddFile);

        await _dbContext.SaveChangesAsync();

        if (File.Exists(tempFilePath))
        {
            File.Delete(tempFilePath);
        }

        return parseResult;
    }

    public async Task<IReadOnlyList<DddFileDto>> GetAllAsync()
    {
        return await _dbContext.DddFiles
            .AsNoTracking()
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new DddFileDto
            {
                Id = x.Id,
                FileName = x.FileName,
                UploadedAtUtc = x.UploadedAtUtc
            })
            .ToListAsync();
    }

    public async Task<DddFileDetailsDto?> GetByIdAsync(Guid id)
    {
        var dddFile = await _dbContext.DddFiles
            .AsNoTracking()
            .Include(x => x.DriverActivities)
            .Include(x => x.CountryEntries)
            .Include(x => x.VehicleUses)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (dddFile == null)
        {
            return null;
        }

        return new DddFileDetailsDto
        {
            Id = dddFile.Id,
            FileName = dddFile.FileName,
            UploadedAt = dddFile.UploadedAtUtc,

            DriverActivities = dddFile.DriverActivities
                .OrderBy(x => x.StartUtc)
                .Select(x => new ParsedDriverActivityDto
                {
                    Start = x.StartUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    End = x.EndUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    Activity = x.ActivityType
                })
                .ToList(),

            CountryEntries = dddFile.CountryEntries
                .OrderBy(x => x.EntryTimeUtc)
                .Select(x => new ParsedCountryEntryDto
                {
                    Timestamp = x.EntryTimeUtc
                        .ToString("yyyy-MM-dd HH:mm:ss"),

                    CountryCode = x.CountryCode
                })
                .ToList(),

            VehicleUses = dddFile.VehicleUses
                .OrderBy(x => x.StartUtc)
                .Select(x => new ParsedVehicleUseDto
                {
                    Start = x.StartUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    End = x.EndUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                    VehicleRegistration = x.RegistrationNumber
                })
                .ToList()
        };
    }
}