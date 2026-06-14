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

        foreach (var activity in parsedData.Activities)
        {
            dddFile.DriverActivities.Add(new DriverActivity
            {
                Id = Guid.NewGuid(),
                StartUtc = ToUtc(activity.Start),
                EndUtc = ToUtc(activity.End),
                ActivityType = activity.Activity
            });
        }

        foreach (var vehicle in parsedData.VehicleUses)
        {
            dddFile.VehicleUses.Add(new VehicleUse
            {
                Id = Guid.NewGuid(),
                RegistrationNumber = vehicle.VehicleRegistration,
                StartUtc = ToUtc(vehicle.Start),
                EndUtc = ToUtc(vehicle.End)
            });
        }

        foreach (var country in parsedData.CountryCodeEntries)
        {
            dddFile.CountryEntries.Add(new CountryEntry
            {
                Id = Guid.NewGuid(),
                CountryCode = country.CountryCode,
                EntryTimeUtc = ToUtc(country.Timestamp)
            });
        }

        _dbContext.DddFiles.Add(dddFile);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return dddFile.Id;
    }

    private static DateTime ToUtc(string value)
    {
        if (!DateTime.TryParse(value, out var dateTime))
            return DateTime.UtcNow;

        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }
}