using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;

namespace DriverTime.Application.Services;

public class DddImportService : IDddImportService
{
    public Task<Guid> ImportAsync(
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
                StartUtc = DateTime.TryParse(activity.Start, out var start)
                    ? start
                    : DateTime.UtcNow,

                EndUtc = DateTime.TryParse(activity.End, out var end)
                    ? end
                    : DateTime.UtcNow,

                ActivityType = activity.Activity
            });
        }

        foreach (var vehicle in parsedData.VehicleUses)
        {
            dddFile.VehicleUses.Add(new VehicleUse
            {
                Id = Guid.NewGuid(),

                RegistrationNumber = vehicle.VehicleRegistration,

                StartUtc = DateTime.TryParse(vehicle.Start, out var start)
                    ? start
                    : DateTime.UtcNow,

                EndUtc = DateTime.TryParse(vehicle.End, out var end)
                    ? end
                    : DateTime.UtcNow,

                StartOdometerKm = vehicle.StartOdometerKm,
                EndOdometerKm = vehicle.EndOdometerKm,
                DistanceKm = vehicle.DistanceKm
            });
        }

        foreach (var country in parsedData.CountryCodeEntries)
        {
            dddFile.CountryEntries.Add(new CountryEntry
            {
                Id = Guid.NewGuid(),

                CountryCode = country.CountryCode,
                EntryType = NormalizeCountryEntryType(country.EntryType),

                EntryTimeUtc = DateTime.TryParse(country.Timestamp, out var entryTime)
                    ? entryTime
                    : DateTime.UtcNow
            });
        }

        return Task.FromResult(dddFile.Id);
    }

    private static string NormalizeCountryEntryType(string? entryType)
    {
        if (string.IsNullOrWhiteSpace(entryType))
        {
            return "Unknown";
        }

        var normalized = entryType.Trim().ToUpperInvariant();

        if (normalized.Contains("START") ||
            normalized.Contains("BEGIN") ||
            normalized.Contains("INSERT") ||
            normalized.Contains("ROZPOCZ") ||
            normalized.Contains("WLOZ") ||
            normalized.Contains("WŁOŻ"))
        {
            return "Start";
        }

        if (normalized.Contains("END") ||
            normalized.Contains("FINISH") ||
            normalized.Contains("WITHDRAW") ||
            normalized.Contains("REMOVE") ||
            normalized.Contains("ZAKON") ||
            normalized.Contains("ZAKOŃ".ToUpperInvariant()) ||
            normalized.Contains("WYJEC") ||
            normalized.Contains("WYJĘ".ToUpperInvariant()))
        {
            return "End";
        }

        return "Unknown";
    }
}
