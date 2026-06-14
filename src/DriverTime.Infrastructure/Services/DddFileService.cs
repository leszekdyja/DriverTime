using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DddFileService : IDddFileService
{
    private readonly ApplicationDbContext _context;
    private readonly IDddParserGateway _dddParserGateway;

    public DddFileService(
        ApplicationDbContext context,
        IDddParserGateway dddParserGateway)
    {
        _context = context;
        _dddParserGateway = dddParserGateway;
    }

    public async Task<DddParseResultDto> UploadAndParseAsync(
        Stream fileStream,
        string fileName)
    {
        var tempFilePath = Path.GetTempFileName();

        try
        {
            await using (var file = File.Create(tempFilePath))
            {
                await fileStream.CopyToAsync(file);
            }

            var result = await _dddParserGateway.ParseAsync(tempFilePath);

            var dddFile = new DddFile
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                UploadedAtUtc = DateTime.UtcNow
            };

            foreach (var activity in result.Activities)
            {
                dddFile.DriverActivities.Add(new DriverActivity
                {
                    Id = Guid.NewGuid(),
                    ActivityType = activity.Activity,

                    StartUtc = DateTime.SpecifyKind(
                        DateTime.Parse(activity.Start),
                        DateTimeKind.Utc),

                    EndUtc = DateTime.SpecifyKind(
                        DateTime.Parse(activity.End),
                        DateTimeKind.Utc)
                });
            }

            foreach (var vehicle in result.VehicleUses)
            {
                dddFile.VehicleUses.Add(new VehicleUse
                {
                    Id = Guid.NewGuid(),
                    RegistrationNumber = vehicle.VehicleRegistration,

                    StartUtc = DateTime.SpecifyKind(
                        DateTime.Parse(vehicle.Start),
                        DateTimeKind.Utc),

                    EndUtc = DateTime.SpecifyKind(
                        DateTime.Parse(vehicle.End),
                        DateTimeKind.Utc)
                });
            }

            foreach (var country in result.CountryCodeEntries)
            {
                dddFile.CountryEntries.Add(new CountryEntry
                {
                    Id = Guid.NewGuid(),
                    CountryCode = country.CountryCode,

                    EntryTimeUtc = DateTime.SpecifyKind(
                        DateTime.Parse(country.Timestamp),
                        DateTimeKind.Utc)
                });
            }

            _context.DddFiles.Add(dddFile);

            await _context.SaveChangesAsync();

            return result;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public async Task<IEnumerable<DddFileDto>> GetAllAsync()
    {
        return await _context.DddFiles
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
        var file = await _context.DddFiles
            .Include(x => x.DriverActivities)
            .Include(x => x.VehicleUses)
            .Include(x => x.CountryEntries)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (file is null)
        {
            return null;
        }

        return new DddFileDetailsDto
        {
            Id = file.Id,
            FileName = file.FileName,
            UploadedAtUtc = file.UploadedAtUtc,

            Activities = file.DriverActivities
                .Select(x => new ParsedDriverActivityDto
                {
                    Activity = x.ActivityType,
                    Start = x.StartUtc.ToString("O"),
                    End = x.EndUtc.ToString("O")
                })
                .ToList(),

            VehicleUses = file.VehicleUses
                .Select(x => new ParsedVehicleUseDto
                {
                    VehicleRegistration = x.RegistrationNumber,
                    Start = x.StartUtc.ToString("O"),
                    End = x.EndUtc.ToString("O")
                })
                .ToList(),

            CountryEntries = file.CountryEntries
                .Select(x => new ParsedCountryEntryDto
                {
                    CountryCode = x.CountryCode,
                    Timestamp = x.EntryTimeUtc.ToString("O")
                })
                .ToList()
        };
    }
}