using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningDriverDutyRuleService : IPlanningDriverDutyRuleService
{
    private const int NotesMaxLength = 1000;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PlanningDriverDutyRuleService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<PlanningDriverDutyRuleDto>> GetAsync(
        PlanningDriverDutyRuleFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var query = _dbContext.PlanningDriverDutyRules
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.PlanningDuty)
            .Where(x => x.CompanyId == companyId);

        if (filter.DriverId.HasValue)
        {
            query = query.Where(x => x.DriverId == filter.DriverId.Value);
        }

        if (filter.DutyId.HasValue)
        {
            query = query.Where(x => x.PlanningDutyId == filter.DutyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Type) && TryParseType(filter.Type, out var type))
        {
            query = query.Where(x => x.Type == type);
        }

        if (filter.ActiveOn.HasValue)
        {
            var activeOn = filter.ActiveOn.Value;
            query = query.Where(x => (!x.ValidFrom.HasValue || x.ValidFrom.Value <= activeOn)
                && (!x.ValidTo.HasValue || x.ValidTo.Value >= activeOn));
        }

        return await query
            .OrderBy(x => x.Driver.LastName)
            .ThenBy(x => x.Driver.FirstName)
            .ThenBy(x => x.PlanningDuty.DutyNumber)
            .ThenBy(x => x.Type)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningDriverDutyRuleDto> CreateAsync(
        PlanningDriverDutyRuleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var companyId = _currentUser.CompanyId;
        var now = DateTime.UtcNow;
        var type = ParseType(request.Type);
        await EnsureDriverAndDutyInCompanyAsync(request.DriverId, request.DutyId, companyId, cancellationToken);
        await EnsureDuplicateDoesNotExistAsync(null, request, type, companyId, cancellationToken);

        var rule = new PlanningDriverDutyRule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DriverId = request.DriverId,
            PlanningDutyId = request.DutyId,
            Type = type,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Notes = NormalizeOptional(request.Notes),
            CreatedAt = now,
            CreatedAtUtc = now
        };

        _dbContext.PlanningDriverDutyRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(rule.Id, cancellationToken))!;
    }

    public async Task<PlanningDriverDutyRuleDto?> UpdateAsync(
        Guid id,
        PlanningDriverDutyRuleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var companyId = _currentUser.CompanyId;
        var rule = await _dbContext.PlanningDriverDutyRules
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (rule is null)
        {
            return null;
        }

        var type = ParseType(request.Type);
        await EnsureDriverAndDutyInCompanyAsync(request.DriverId, request.DutyId, companyId, cancellationToken);
        await EnsureDuplicateDoesNotExistAsync(id, request, type, companyId, cancellationToken);

        rule.DriverId = request.DriverId;
        rule.PlanningDutyId = request.DutyId;
        rule.Type = type;
        rule.ValidFrom = request.ValidFrom;
        rule.ValidTo = request.ValidTo;
        rule.Notes = NormalizeOptional(request.Notes);
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var rule = await _dbContext.PlanningDriverDutyRules
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (rule is null)
        {
            return false;
        }

        _dbContext.PlanningDriverDutyRules.Remove(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PlanningDriverDutyRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await _dbContext.PlanningDriverDutyRules
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.PlanningDuty)
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task EnsureDriverAndDutyInCompanyAsync(
        Guid driverId,
        Guid dutyId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var driverExists = await _dbContext.Drivers.AnyAsync(x => x.Id == driverId && x.CompanyId == companyId, cancellationToken);
        if (!driverExists)
        {
            throw new PlanningDutyValidationException(new[] { "Kierowca nie należy do bieżącej firmy." });
        }

        var dutyExists = await _dbContext.PlanningDuties.AnyAsync(x => x.Id == dutyId && x.CompanyId == companyId, cancellationToken);
        if (!dutyExists)
        {
            throw new PlanningDutyValidationException(new[] { "Służba nie należy do bieżącej firmy." });
        }
    }

    private async Task EnsureDuplicateDoesNotExistAsync(
        Guid? currentId,
        PlanningDriverDutyRuleRequestDto request,
        PlanningDriverDutyRuleType type,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var duplicateExists = await _dbContext.PlanningDriverDutyRules.AnyAsync(x =>
            x.CompanyId == companyId
            && x.Id != currentId
            && x.DriverId == request.DriverId
            && x.PlanningDutyId == request.DutyId
            && x.Type == type
            && x.ValidFrom == request.ValidFrom
            && x.ValidTo == request.ValidTo,
            cancellationToken);

        if (duplicateExists)
        {
            throw new PlanningDutyValidationException(new[] { "Taka reguła już istnieje." });
        }
    }

    private static void ValidateRequest(PlanningDriverDutyRuleRequestDto request)
    {
        var errors = new List<string>();
        if (request.DriverId == Guid.Empty)
        {
            errors.Add("Wybierz kierowcę.");
        }

        if (request.DutyId == Guid.Empty)
        {
            errors.Add("Wybierz służbę.");
        }

        if (!TryParseType(request.Type, out _))
        {
            errors.Add("Nieznany typ reguły.");
        }

        if (request.ValidFrom.HasValue && request.ValidTo.HasValue && request.ValidFrom.Value > request.ValidTo.Value)
        {
            errors.Add("Data początku obowiązywania nie może być późniejsza niż data końca.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > NotesMaxLength)
        {
            errors.Add("Notatka reguły jest za długa.");
        }

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }

    private static PlanningDriverDutyRuleType ParseType(string? value) =>
        TryParseType(value, out var type)
            ? type
            : throw new PlanningDutyValidationException(new[] { "Nieznany typ reguły." });

    private static bool TryParseType(string? value, out PlanningDriverDutyRuleType type) =>
        Enum.TryParse(value, ignoreCase: true, out type);

    private static PlanningDriverDutyRuleDto ToDto(PlanningDriverDutyRule rule) => new()
    {
        Id = rule.Id,
        DriverId = rule.DriverId,
        DriverFullName = FormatDriverName(rule.Driver),
        DutyId = rule.PlanningDutyId,
        DutyNumber = rule.PlanningDuty.DutyNumber,
        DutyName = rule.PlanningDuty.Name,
        Type = rule.Type.ToString(),
        ValidFrom = rule.ValidFrom,
        ValidTo = rule.ValidTo,
        Notes = rule.Notes,
        CreatedAtUtc = rule.CreatedAtUtc,
        UpdatedAtUtc = rule.UpdatedAtUtc
    };

    private static string FormatDriverName(Driver driver)
    {
        var name = $"{driver.LastName} {driver.FirstName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? driver.CardNumber : name;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
