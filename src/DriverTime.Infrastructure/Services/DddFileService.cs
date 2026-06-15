using System.Security.Cryptography;
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
    private readonly ICurrentUserService _currentUser;

    public DddFileService(
        DriverTimeDbContext dbContext,
        IDddParserGateway dddParserGateway,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _dddParserGateway = dddParserGateway;
        _currentUser = currentUser;
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
            var fileHash = await CalculateHashAsync(tempFilePath);

            if (await _dbContext.DddFiles.AnyAsync(x =>
                x.CompanyId == _currentUser.CompanyId && x.FileHash == fileHash))
            {
                throw new InvalidOperationException("Ten plik DDD zostal juz zaimportowany.");
            }

            var parseResult = await _dddParserGateway.ParseAsync(tempFilePath);
            var cardNumber = NormalizeCardNumber(parseResult.Driver.CardNumber);

            if (string.IsNullOrWhiteSpace(cardNumber))
            {
                throw new InvalidOperationException(
                    "Nie udalo sie odczytac numeru karty kierowcy z pliku DDD.");
            }

            var driver = await _dbContext.Drivers.FirstOrDefaultAsync(x =>
                x.CompanyId == _currentUser.CompanyId && x.CardNumber == cardNumber);
            var driverCreated = driver is null;

            if (driver is null)
            {
                driver = CreateDriver(parseResult.Driver, cardNumber);
                _dbContext.Drivers.Add(driver);
            }
            else
            {
                UpdateMissingDriverData(driver, parseResult.Driver);
            }

            var dddFile = new DddFile
            {
                Id = Guid.NewGuid(),
                CompanyId = _currentUser.CompanyId,
                DriverId = driver.Id,
                FileName = originalFileName,
                FileHash = fileHash,
                UploadedAtUtc = DateTime.UtcNow,
                DriverCardNumber = cardNumber,
                DriverFirstName = driver.FirstName,
                DriverLastName = driver.LastName,
                DriverCreatedDuringImport = driverCreated
            };

            AddActivities(dddFile, parseResult.Activities);
            AddVehicleUses(dddFile, parseResult.VehicleUses);
            AddCountryEntries(dddFile, parseResult.CountryCodeEntries);
            _dbContext.DddFiles.Add(dddFile);

            await _dbContext.SaveChangesAsync();

            parseResult.ImportId = dddFile.Id;
            parseResult.DriverCreated = driverCreated;
            parseResult.ImportMessage = driverCreated
                ? "Utworzono nowego kierowce."
                : "Import przypisano do istniejacego kierowcy.";

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
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .OrderByDescending(x => x.UploadedAtUtc)
            .Select(x => new DddFileDto
            {
                Id = x.Id,
                FileName = x.FileName,
                DriverFirstName = x.DriverFirstName,
                DriverLastName = x.DriverLastName,
                DriverCardNumber = x.DriverCardNumber,
                DriverStatus = x.DriverCreatedDuringImport ? "new" : "existing",
                UploadedAtUtc = x.UploadedAtUtc,
                ActivitiesCount = x.DriverActivities.Count
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
            .FirstOrDefaultAsync(x =>
                x.Id == id && x.CompanyId == _currentUser.CompanyId);

        if (dddFile is null)
        {
            return null;
        }

        return new DddFileDetailsDto
        {
            Id = dddFile.Id,
            FileName = dddFile.FileName,
            DriverFirstName = dddFile.DriverFirstName,
            DriverLastName = dddFile.DriverLastName,
            DriverCardNumber = dddFile.DriverCardNumber,
            UploadedAtUtc = dddFile.UploadedAtUtc,
            DriverActivities = dddFile.DriverActivities
                .OrderBy(x => x.StartUtc)
                .Select(x => new ParsedDriverActivityDto
                {
                    Start = x.StartUtc.ToString("O"),
                    End = x.EndUtc.ToString("O"),
                    Activity = x.ActivityType,
                    ActivityCode = x.ActivityType
                })
                .ToList(),
            CountryEntries = dddFile.CountryEntries
                .OrderBy(x => x.EntryTimeUtc)
                .Select(x => new ParsedCountryEntryDto
                {
                    Timestamp = x.EntryTimeUtc.ToString("O"),
                    CountryCode = x.CountryCode
                })
                .ToList(),
            VehicleUses = dddFile.VehicleUses
                .OrderBy(x => x.StartUtc)
                .Select(x => new ParsedVehicleUseDto
                {
                    Start = x.StartUtc.ToString("O"),
                    End = x.EndUtc.ToString("O"),
                    VehicleRegistration = x.RegistrationNumber
                })
                .ToList()
        };
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.DddFiles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == id && x.CompanyId == _currentUser.CompanyId,
                cancellationToken);

        if (!exists)
        {
            return false;
        }

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        await _dbContext.DriverActivities
            .Where(x => x.DddFileId == id)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.VehicleUses
            .Where(x => x.DddFileId == id)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.CountryEntries
            .Where(x => x.DddFileId == id)
            .ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DddFiles
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private Driver CreateDriver(ParsedDriverDto parsedDriver, string cardNumber)
    {
        return new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            CardNumber = cardNumber,
            FirstName = parsedDriver.FirstName.Trim(),
            LastName = parsedDriver.LastName.Trim(),
            CardExpiryDate = ParseDate(parsedDriver.CardExpiryDate),
            CardIssuingCountry = parsedDriver.CardIssuingCountry.Trim()
        };
    }

    private static async Task<string> CalculateHashAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream);

        return Convert.ToHexString(hash);
    }

    private static string NormalizeCardNumber(string value) =>
        value.Trim().ToUpperInvariant();

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(value, out var date)
            ? DateTime.SpecifyKind(date.Date, DateTimeKind.Utc)
            : null;
    }

    private static DateTime? ParseDateTime(string value)
    {
        return DateTime.TryParse(value, out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : null;
    }

    private static void UpdateMissingDriverData(
        Driver driver,
        ParsedDriverDto parsedDriver)
    {
        if (string.IsNullOrWhiteSpace(driver.FirstName)
            && !string.IsNullOrWhiteSpace(parsedDriver.FirstName))
        {
            driver.FirstName = parsedDriver.FirstName.Trim();
        }

        if (string.IsNullOrWhiteSpace(driver.LastName)
            && !string.IsNullOrWhiteSpace(parsedDriver.LastName))
        {
            driver.LastName = parsedDriver.LastName.Trim();
        }

        if (!driver.CardExpiryDate.HasValue)
        {
            driver.CardExpiryDate = ParseDate(parsedDriver.CardExpiryDate);
        }

        if (string.IsNullOrWhiteSpace(driver.CardIssuingCountry)
            && !string.IsNullOrWhiteSpace(parsedDriver.CardIssuingCountry))
        {
            driver.CardIssuingCountry = parsedDriver.CardIssuingCountry.Trim();
        }
    }

    private static void AddActivities(
        DddFile dddFile,
        IEnumerable<ParsedDriverActivityDto> activities)
    {
        foreach (var activity in activities)
        {
            var start = ParseDateTime(activity.Start);
            var end = ParseDateTime(activity.End);

            if (!start.HasValue || !end.HasValue || end <= start)
            {
                continue;
            }

            dddFile.DriverActivities.Add(new DriverActivity
            {
                Id = Guid.NewGuid(),
                StartUtc = start.Value,
                EndUtc = end.Value,
                ActivityType = string.IsNullOrWhiteSpace(activity.ActivityCode)
                    ? activity.Activity
                    : activity.ActivityCode
            });
        }
    }

    private static void AddVehicleUses(
        DddFile dddFile,
        IEnumerable<ParsedVehicleUseDto> vehicleUses)
    {
        foreach (var vehicleUse in vehicleUses)
        {
            var start = ParseDateTime(vehicleUse.Start);
            var end = ParseDateTime(vehicleUse.End);

            if (!start.HasValue || !end.HasValue || end <= start)
            {
                continue;
            }

            dddFile.VehicleUses.Add(new VehicleUse
            {
                Id = Guid.NewGuid(),
                StartUtc = start.Value,
                EndUtc = end.Value,
                RegistrationNumber = vehicleUse.VehicleRegistration
            });
        }
    }

    private static void AddCountryEntries(
        DddFile dddFile,
        IEnumerable<ParsedCountryEntryDto> countryEntries)
    {
        foreach (var countryEntry in countryEntries)
        {
            var timestamp = ParseDateTime(countryEntry.Timestamp);

            if (!timestamp.HasValue)
            {
                continue;
            }

            dddFile.CountryEntries.Add(new CountryEntry
            {
                Id = Guid.NewGuid(),
                EntryTimeUtc = timestamp.Value,
                CountryCode = countryEntry.CountryCode
            });
        }
    }
}
