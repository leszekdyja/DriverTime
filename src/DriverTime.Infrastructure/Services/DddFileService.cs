using System.Security.Cryptography;
using DriverTime.Application.Compliance;
using DriverTime.Application.DDD.Exceptions;
using DriverTime.Application.DDD.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

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
        catch (DuplicateDddFileException exception)
        {
            await _importMonitoringService.MarkDuplicateAsync(
                monitoringEntry.Id,
                exception.Message);
            TryDeleteFile(retryFilePath);

            throw;
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
        catch (DuplicateDddFileException exception)
        {
            await _importMonitoringService.MarkDuplicateAsync(
                monitoringId,
                exception.Message,
                cancellationToken);
            TryDeleteFile(entry.StoredFilePath);

            return false;
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
                    CountryCode = x.CountryCode,
                    EntryType = x.EntryType
                })
                .ToList(),
            VehicleUses = dddFile.VehicleUses
                .OrderBy(x => x.StartUtc)
                .Select(x => new ParsedVehicleUseDto
                {
                    Start = x.StartUtc.ToString("O"),
                    End = x.EndUtc.ToString("O"),
                    VehicleRegistration = x.RegistrationNumber,
                    StartOdometerKm = x.StartOdometerKm,
                    EndOdometerKm = x.EndOdometerKm,
                    DistanceKm = x.DistanceKm
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
            throw new DuplicateDddFileException();
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

        var nowUtc = DateTime.UtcNow;
        var originalActivitiesCount = parseResult.Activities.Count;
        var originalVehicleUsesCount = parseResult.VehicleUses.Count;

        parseResult.Activities = parseResult.Activities
            .Where(x => IsValidDriverActivity(x, nowUtc))
            .ToList();
        parseResult.VehicleUses = parseResult.VehicleUses
            .Where(x => IsValidVehicleUse(x, nowUtc))
            .ToList();

        var rejectedActivitiesCount = originalActivitiesCount - parseResult.Activities.Count;
        var rejectedVehicleUsesCount = originalVehicleUsesCount - parseResult.VehicleUses.Count;

        if (rejectedActivitiesCount > 0 || rejectedVehicleUsesCount > 0)
        {
            _logger.LogWarning(
                "DDD import skipped invalid records for file {FileName}. RejectedDriverActivities={RejectedDriverActivities}, RejectedVehicleUses={RejectedVehicleUses}.",
                originalFileName,
                rejectedActivitiesCount,
                rejectedVehicleUsesCount);
        }

        AddActivities(dddFile, parseResult.Activities, nowUtc);
        AddVehicleUses(dddFile, parseResult.VehicleUses, nowUtc);
        AddCountryEntries(dddFile, parseResult.CountryCodeEntries);
        await AddMissingVehiclesAsync(companyId, parseResult.VehicleUses);
        _dbContext.DddFiles.Add(dddFile);

        driver = await SaveImportChangesHandlingDuplicatesAsync(
            companyId,
            cardNumber,
            parseResult.Driver,
            driver,
            dddFile,
            driverCreated);
        driverCreated = dddFile.DriverCreatedDuringImport;

        parseResult.ImportId = dddFile.Id;
        parseResult.DriverCreated = driverCreated;
        parseResult.ImportMessage = driverCreated
            ? "Utworzono nowego kierowce."
            : "Import przypisano do istniejacego kierowcy.";

        await EvaluateComplianceAfterImportAsync(companyId, driver.Id);

        await _importMonitoringService.MarkCompletedAsync(monitoringId);

        return parseResult;
    }

    private async Task<Driver> SaveImportChangesHandlingDuplicatesAsync(
        Guid companyId,
        string cardNumber,
        ParsedDriverDto parsedDriver,
        Driver driver,
        DddFile dddFile,
        bool driverCreated)
    {
        try
        {
            await _dbContext.SaveChangesAsync();
            return driver;
        }
        catch (DbUpdateException exception) when (IsDuplicateDddFileHash(exception))
        {
            throw new DuplicateDddFileException();
        }
        catch (DbUpdateException exception) when (driverCreated && IsDuplicateDriverCardNumber(exception))
        {
            _logger.LogInformation(
                "DDD import detected concurrent driver creation for company {CompanyId}, card {CardNumber}. Reusing existing driver.",
                companyId,
                cardNumber);

            DetachEntity(driver);

            var existingDriver = await _dbContext.Drivers
                .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.CardNumber == cardNumber);

            if (existingDriver is null)
            {
                throw;
            }

            ApplyExistingDriverAfterConcurrentInsert(dddFile, existingDriver, parsedDriver);

            try
            {
                await _dbContext.SaveChangesAsync();
                return existingDriver;
            }
            catch (DbUpdateException retryException) when (IsDuplicateDddFileHash(retryException))
            {
                throw new DuplicateDddFileException();
            }
        }
    }

    internal static void ApplyExistingDriverAfterConcurrentInsert(
        DddFile dddFile,
        Driver existingDriver,
        ParsedDriverDto parsedDriver)
    {
        UpdateMissingDriverData(existingDriver, parsedDriver);
        dddFile.DriverId = existingDriver.Id;
        dddFile.DriverFirstName = existingDriver.FirstName;
        dddFile.DriverLastName = existingDriver.LastName;
        dddFile.DriverCreatedDuringImport = false;
    }

    private void DetachEntity(object entity)
    {
        var entry = _dbContext.Entry(entity);

        if (entry.State != EntityState.Detached)
        {
            entry.State = EntityState.Detached;
        }
    }

    internal static bool IsDuplicateDddFileHash(DbUpdateException exception) =>
        IsUniqueConstraintViolation(exception, "IX_DddFiles_CompanyId_FileHash");

    internal static bool IsDuplicateDriverCardNumber(DbUpdateException exception) =>
        IsUniqueConstraintViolation(exception, "IX_Drivers_CompanyId_CardNumber");

    internal static bool IsDuplicateDddFileHashConstraint(string? constraintName) =>
        string.Equals(constraintName, "IX_DddFiles_CompanyId_FileHash", StringComparison.Ordinal);

    internal static bool IsDuplicateDriverCardNumberConstraint(string? constraintName) =>
        string.Equals(constraintName, "IX_Drivers_CompanyId_CardNumber", StringComparison.Ordinal);

    private static bool IsUniqueConstraintViolation(
        DbUpdateException exception,
        string constraintName)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, constraintName, StringComparison.Ordinal);
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

    internal static void AddActivities(
        DddFile dddFile,
        IEnumerable<ParsedDriverActivityDto> activities,
        DateTime nowUtc)
    {
        foreach (var activity in activities)
        {
            var start = ParseDateTime(activity.Start);
            var end = ParseDateTime(activity.End);

            if (!start.HasValue
                || !end.HasValue
                || !DriverActivityDateValidator.IsValid(start.Value, end.Value, nowUtc))
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

    internal static void AddVehicleUses(
        DddFile dddFile,
        IEnumerable<ParsedVehicleUseDto> vehicleUses,
        DateTime nowUtc)
    {
        foreach (var vehicleUse in vehicleUses)
        {
            var start = ParseDateTime(vehicleUse.Start);
            var end = ParseDateTime(vehicleUse.End);

            if (!start.HasValue
                || !end.HasValue
                || !VehicleUseDateValidator.IsValid(start.Value, end.Value, nowUtc))
            {
                continue;
            }

            dddFile.VehicleUses.Add(new VehicleUse
            {
                Id = Guid.NewGuid(),
                StartUtc = start.Value,
                EndUtc = end.Value,
                RegistrationNumber = vehicleUse.VehicleRegistration,
                StartOdometerKm = vehicleUse.StartOdometerKm,
                EndOdometerKm = vehicleUse.EndOdometerKm,
                DistanceKm = vehicleUse.DistanceKm
            });
        }
    }

    private async Task AddMissingVehiclesAsync(
        Guid companyId,
        IEnumerable<ParsedVehicleUseDto> vehicleUses)
    {
        var registrationNumbers = vehicleUses
            .Select(x => NormalizeVehicleRegistration(x.VehicleRegistration))
            .Where(IsUsableVehicleRegistration)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (registrationNumbers.Count == 0)
        {
            _logger.LogInformation(
                "DDD import vehicle sync completed for company {CompanyId}. UniqueRegistrations={UniqueRegistrations}, ExistingVehicles={ExistingVehicles}, CreatedVehicles={CreatedVehicles}.",
                companyId,
                0,
                0,
                0);

            return;
        }

        var vehicles = _dbContext.Set<Vehicle>();
        var existingRegistrationNumbers = await vehicles
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.RegistrationNumber)
            .ToListAsync();

        var existing = existingRegistrationNumbers
            .Select(NormalizeVehicleRegistration)
            .Where(IsUsableVehicleRegistration)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        registrationNumbers = registrationNumbers
            .Where(x => !HasFullerVehicleRegistrationVariant(
                x,
                registrationNumbers.Concat(existing)))
            .ToList();
        var existingCount = registrationNumbers.Count(existing.Contains);
        var createdCount = 0;

        foreach (var registrationNumber in registrationNumbers)
        {
            if (existing.Contains(registrationNumber)
                || HasFullerVehicleRegistrationVariant(registrationNumber, existing))
            {
                continue;
            }

            vehicles.Add(new Vehicle
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                RegistrationNumber = registrationNumber,
                Vin = string.Empty,
                Active = true
            });

            existing.Add(registrationNumber);
            createdCount++;
        }

        _logger.LogInformation(
            "DDD import vehicle sync completed for company {CompanyId}. UniqueRegistrations={UniqueRegistrations}, ExistingVehicles={ExistingVehicles}, CreatedVehicles={CreatedVehicles}.",
            companyId,
            registrationNumbers.Count,
            existingCount,
            createdCount);
    }

    private static string NormalizeVehicleRegistration(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Join(
                " ",
                value.Trim()
                    .ToUpperInvariant()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsUsableVehicleRegistration(string value)
    {
        return GetVehicleRegistrationCompactValue(value).Length >= 5;
    }

    private static bool IsValidVehicleUse(
        ParsedVehicleUseDto vehicleUse,
        DateTime nowUtc)
    {
        var start = ParseDateTime(vehicleUse.Start);
        var end = ParseDateTime(vehicleUse.End);

        return start.HasValue
            && end.HasValue
            && VehicleUseDateValidator.IsValid(start.Value, end.Value, nowUtc);
    }

    private static bool IsValidDriverActivity(
        ParsedDriverActivityDto activity,
        DateTime nowUtc)
    {
        var start = ParseDateTime(activity.Start);
        var end = ParseDateTime(activity.End);

        return start.HasValue
            && end.HasValue
            && DriverActivityDateValidator.IsValid(start.Value, end.Value, nowUtc);
    }

    private static bool HasFullerVehicleRegistrationVariant(
        string registrationNumber,
        IEnumerable<string> candidates)
    {
        var compact = GetVehicleRegistrationCompactValue(registrationNumber);

        return candidates
            .Select(GetVehicleRegistrationCompactValue)
            .Any(candidate =>
                candidate.Length > compact.Length
                && candidate.EndsWith(compact, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetVehicleRegistrationCompactValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value
                .Where(x => !char.IsWhiteSpace(x))
                .ToArray())
                .ToUpperInvariant();
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
                CountryCode = countryEntry.CountryCode,
                EntryType = NormalizeCountryEntryType(countryEntry.EntryType)
            });
        }
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
