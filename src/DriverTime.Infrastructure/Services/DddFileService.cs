using System.Security.Cryptography;
using DriverTime.Application.Compliance;
using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DriverTime.Infrastructure.Services;

public class DddFileService : IDddFileService
{
    private const string DevCompanyName = "DriverTime Dev Company";
    private const string DevCompanyVatNumber = "DEV";
    private const string DevCompanyAddress = "Local development";

    private readonly DriverTimeDbContext _dbContext;
    private readonly IDddParserGateway _dddParserGateway;
    private readonly ICurrentUserService _currentUser;
    private readonly IDddImportMonitoringService _importMonitoringService;
    private readonly IViolationDetectionService _violationDetectionService;
    private readonly IComplianceEvaluationService _complianceEvaluationService;
    private readonly ILogger<DddFileService> _logger;
    private readonly ImportRetryOptions _retryOptions;

    public DddFileService(
        DriverTimeDbContext dbContext,
        IDddParserGateway dddParserGateway,
        ICurrentUserService currentUser,
        IDddImportMonitoringService importMonitoringService,
        IViolationDetectionService violationDetectionService,
        IComplianceEvaluationService complianceEvaluationService,
        ILogger<DddFileService> logger,
        IOptions<ImportRetryOptions> retryOptions)
    {
        _dbContext = dbContext;
        _dddParserGateway = dddParserGateway;
        _currentUser = currentUser;
        _importMonitoringService = importMonitoringService;
        _violationDetectionService = violationDetectionService;
        _complianceEvaluationService = complianceEvaluationService;
        _logger = logger;
        _retryOptions = retryOptions.Value;
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

        var monitoringEntry = await _importMonitoringService.CreateAsync(originalFileName);
        var retryFilePath = await StoreRetryFileAsync(tempFilePath, monitoringEntry.Id, originalFileName);
        await _importMonitoringService.SetStoredFilePathAsync(
            monitoringEntry.Id,
            retryFilePath);

        try
        {
            var result = await ImportFromPathAsync(
                tempFilePath,
                originalFileName,
                monitoringEntry.Id,
                companyIdOverride: null);

            TryDeleteFile(retryFilePath);

            return result;
        }
        catch (Exception exception)
        {
            await _importMonitoringService.MarkFailedAsync(
                monitoringEntry.Id,
                exception.Message);

            throw;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    public async Task<bool> RetryImportAsync(
        Guid monitoringId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.DddImportMonitoringEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == monitoringId, cancellationToken);

        if (entry is null ||
            entry.Status != DddImportMonitoringStatus.Failed ||
            entry.RetryCount >= _retryOptions.MaxRetryCount)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.StoredFilePath) ||
            !File.Exists(entry.StoredFilePath))
        {
            await _importMonitoringService.MarkFailedAsync(
                monitoringId,
                "Nie mozna ponowic importu, poniewaz brakuje zachowanej kopii pliku DDD.",
                cancellationToken);

            return false;
        }

        await _importMonitoringService.MarkRetryProcessingAsync(
            monitoringId,
            cancellationToken);

        try
        {
            await ImportFromPathAsync(
                entry.StoredFilePath,
                entry.FileName,
                monitoringId,
                entry.CompanyId);

            TryDeleteFile(entry.StoredFilePath);

            return true;
        }
        catch (Exception exception)
        {
            await _importMonitoringService.MarkFailedAsync(
                monitoringId,
                exception.Message,
                cancellationToken);

            return false;
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

    private async Task<DddParseResultDto> ImportFromPathAsync(
        string filePath,
        string originalFileName,
        Guid monitoringId,
        Guid? companyIdOverride)
    {
        await _importMonitoringService.MarkProcessingAsync(monitoringId);

        var companyId = companyIdOverride ?? await ResolveImportCompanyIdAsync();
        var fileHash = await CalculateHashAsync(filePath);

        if (await _dbContext.DddFiles.AnyAsync(x =>
            x.CompanyId == companyId && x.FileHash == fileHash))
        {
            throw new InvalidOperationException("Ten plik DDD zostal juz zaimportowany.");
        }

        var parseResult = await _dddParserGateway.ParseAsync(filePath);
        var cardNumber = NormalizeCardNumber(parseResult.Driver.CardNumber);

        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            throw new InvalidOperationException(
                "Nie udalo sie odczytac numeru karty kierowcy z pliku DDD.");
        }

        var driver = await _dbContext.Drivers.FirstOrDefaultAsync(x =>
            x.CompanyId == companyId && x.CardNumber == cardNumber);
        var driverCreated = driver is null;

        if (driver is null)
        {
            driver = CreateDriver(parseResult.Driver, cardNumber, companyId);
            _dbContext.Drivers.Add(driver);
        }
        else
        {
            UpdateMissingDriverData(driver, parseResult.Driver);
        }

        var dddFile = new DddFile
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
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

        await DetectViolationsAfterImportAsync(dddFile.Id);
        await EvaluateComplianceAfterImportAsync(companyId, driver.Id);

        await _importMonitoringService.MarkCompletedAsync(monitoringId);

        return parseResult;
    }

    private async Task DetectViolationsAfterImportAsync(Guid importId)
    {
        try
        {
            await _violationDetectionService.DetectAfterImportAsync(importId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Violation detection failed after DDD import {ImportId}.",
                importId);
        }
    }

    private async Task EvaluateComplianceAfterImportAsync(
        Guid companyId,
        Guid driverId)
    {
        try
        {
            await _complianceEvaluationService.EvaluateForDriverAsync(
                companyId,
                driverId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Compliance evaluation failed after DDD import for driver {DriverId}.",
                driverId);
        }
    }

    private static async Task<string> StoreRetryFileAsync(
        string sourcePath,
        Guid monitoringId,
        string originalFileName)
    {
        var retryDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "import-retry");
        Directory.CreateDirectory(retryDirectory);

        var safeFileName = string.Join(
            "_",
            originalFileName.Split(Path.GetInvalidFileNameChars()));
        var retryFilePath = Path.Combine(
            retryDirectory,
            $"{monitoringId:N}_{safeFileName}");

        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(retryFilePath);
        await source.CopyToAsync(destination);

        return retryFilePath;
    }

    private static void TryDeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // Retry cache cleanup should never break a completed import.
        }
    }

    private async Task<Guid> ResolveImportCompanyIdAsync()
    {
        if (_currentUser.IsAuthenticated && _currentUser.CompanyId != Guid.Empty)
        {
            var existingCompanyId = await _dbContext.Companies
                .AsNoTracking()
                .Where(x => x.Id == _currentUser.CompanyId)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            if (existingCompanyId != Guid.Empty)
            {
                return existingCompanyId;
            }
        }

        var devCompanyId = await _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.VatNumber == DevCompanyVatNumber)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        if (devCompanyId != Guid.Empty)
        {
            return devCompanyId;
        }

        var devCompany = new Company
        {
            Id = Guid.NewGuid(),
            Name = DevCompanyName,
            VatNumber = DevCompanyVatNumber,
            Address = DevCompanyAddress
        };

        _dbContext.Companies.Add(devCompany);
        await _dbContext.SaveChangesAsync();

        return devCompany.Id;
    }

    private Driver CreateDriver(
        ParsedDriverDto parsedDriver,
        string cardNumber,
        Guid companyId)
    {
        return new Driver
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
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
