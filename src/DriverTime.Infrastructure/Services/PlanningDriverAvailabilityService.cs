using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningDriverAvailabilityService : IPlanningDriverAvailabilityService
{
    private const int NoteMaxLength = 1000;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PlanningDriverAvailabilityService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<PlanningDriverAvailabilityDto>> GetAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        ValidateDateRange(dateFrom, dateTo);
        var companyId = _currentUser.CompanyId;

        return await _dbContext.PlanningDriverAvailabilities
            .AsNoTracking()
            .Include(x => x.Driver)
            .Where(x => x.CompanyId == companyId && x.DateFrom <= dateTo && x.DateTo >= dateFrom)
            .OrderBy(x => x.DateFrom)
            .ThenBy(x => x.Driver.LastName)
            .ThenBy(x => x.Driver.FirstName)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningDriverAvailabilityDto> CreateAsync(
        PlanningDriverAvailabilityCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        var companyId = _currentUser.CompanyId;

        var driver = await _dbContext.Drivers
            .Where(x => x.Id == request.DriverId && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (driver is null)
        {
            throw new PlanningDutyValidationException(new[] { "Nie można dodać dostępności kierowcy spoza aktualnej firmy." });
        }

        var now = DateTime.UtcNow;
        var availability = new PlanningDriverAvailability
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DriverId = request.DriverId,
            Driver = driver,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Type = ParseType(request.Type),
            Note = NormalizeOptional(request.Note),
            CreatedAt = now,
            CreatedAtUtc = now
        };

        _dbContext.PlanningDriverAvailabilities.Add(availability);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(availability);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var availability = await _dbContext.PlanningDriverAvailabilities
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (availability is null)
        {
            return false;
        }

        _dbContext.PlanningDriverAvailabilities.Remove(availability);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateCreateRequest(PlanningDriverAvailabilityCreateRequestDto request)
    {
        var errors = new List<string>();

        if (request.DriverId == Guid.Empty)
        {
            errors.Add("Wybierz kierowcę.");
        }

        if (request.DateFrom == default)
        {
            errors.Add("Podaj datę od.");
        }

        if (request.DateTo == default)
        {
            errors.Add("Podaj datę do.");
        }

        if (request.DateFrom > request.DateTo)
        {
            errors.Add("Data od nie może być późniejsza niż data do.");
        }

        if (TryParseType(request.Type) is null)
        {
            errors.Add("Nieznany typ dostępności kierowcy.");
        }

        if (!string.IsNullOrWhiteSpace(request.Note) && request.Note.Trim().Length > NoteMaxLength)
        {
            errors.Add("Notatka dostępności jest za długa.");
        }

        ThrowIfErrors(errors);
    }

    private static void ValidateDateRange(DateOnly dateFrom, DateOnly dateTo)
    {
        var errors = new List<string>();
        if (dateFrom == default)
        {
            errors.Add("Podaj datę od.");
        }

        if (dateTo == default)
        {
            errors.Add("Podaj datę do.");
        }

        if (dateFrom > dateTo)
        {
            errors.Add("Data od nie może być późniejsza niż data do.");
        }

        ThrowIfErrors(errors);
    }

    private static PlanningDriverAvailabilityType ParseType(string? value) =>
        TryParseType(value) ?? throw new PlanningDutyValidationException(new[] { "Nieznany typ dostępności kierowcy." });

    private static PlanningDriverAvailabilityType? TryParseType(string? value) =>
        Enum.TryParse<PlanningDriverAvailabilityType>(value, ignoreCase: true, out var type)
            ? type
            : null;

    private static PlanningDriverAvailabilityDto ToDto(PlanningDriverAvailability availability) => new()
    {
        Id = availability.Id,
        DriverId = availability.DriverId,
        DriverFullName = FormatDriverName(availability.Driver),
        DateFrom = availability.DateFrom,
        DateTo = availability.DateTo,
        Type = availability.Type.ToString(),
        Note = availability.Note,
        CreatedAtUtc = availability.CreatedAtUtc
    };

    private static string FormatDriverName(Driver driver)
    {
        var name = $"{driver.FirstName} {driver.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? driver.CardNumber : name;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }
}
