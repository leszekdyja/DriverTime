using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningDriverPairService(DriverTimeDbContext dbContext, ICurrentUserService currentUser) : IPlanningDriverPairService
{
    public Task<List<PlanningDriverPairDto>> GetAsync(CancellationToken cancellationToken = default) =>
        Query().OrderBy(x => x.FirstDriver.LastName).ThenBy(x => x.SecondDriver.LastName).Select(x => Map(x)).ToListAsync(cancellationToken);

    public async Task<PlanningDriverPairDto> CreateAsync(PlanningDriverPairRequestDto request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, null, cancellationToken);
        var pair = new PlanningDriverPair
        {
            Id = Guid.NewGuid(), CompanyId = currentUser.CompanyId,
            FirstDriverId = request.FirstDriverId, SecondDriverId = request.SecondDriverId,
            IsNightDutyPair = request.IsNightDutyPair, PreventSameShift = request.PreventSameShift,
            IsActive = request.IsActive, Notes = Normalize(request.Notes), CreatedAt = DateTime.UtcNow
        };
        dbContext.PlanningDriverPairs.Add(pair);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await Query().Where(x => x.Id == pair.Id).Select(x => Map(x)).SingleAsync(cancellationToken);
    }

    public async Task<PlanningDriverPairDto?> UpdateAsync(Guid id, PlanningDriverPairRequestDto request, CancellationToken cancellationToken = default)
    {
        var pair = await dbContext.PlanningDriverPairs.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue
                || (x.FirstDriver.OperatingCompanyId == currentUser.OperatingCompanyId.Value
                    && x.SecondDriver.OperatingCompanyId == currentUser.OperatingCompanyId.Value)), cancellationToken);
        if (pair is null) return null;
        await ValidateAsync(request, id, cancellationToken);
        pair.FirstDriverId = request.FirstDriverId; pair.SecondDriverId = request.SecondDriverId;
        pair.IsNightDutyPair = request.IsNightDutyPair; pair.PreventSameShift = request.PreventSameShift;
        pair.IsActive = request.IsActive; pair.Notes = Normalize(request.Notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await Query().Where(x => x.Id == id).Select(x => Map(x)).SingleAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pair = await dbContext.PlanningDriverPairs.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue
                || (x.FirstDriver.OperatingCompanyId == currentUser.OperatingCompanyId.Value
                    && x.SecondDriver.OperatingCompanyId == currentUser.OperatingCompanyId.Value)), cancellationToken);
        if (pair is null) return false;
        dbContext.PlanningDriverPairs.Remove(pair); await dbContext.SaveChangesAsync(cancellationToken); return true;
    }

    private IQueryable<PlanningDriverPair> Query() => dbContext.PlanningDriverPairs.AsNoTracking()
        .Include(x => x.FirstDriver).Include(x => x.SecondDriver).Where(x => x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue
                || (x.FirstDriver.OperatingCompanyId == currentUser.OperatingCompanyId.Value
                    && x.SecondDriver.OperatingCompanyId == currentUser.OperatingCompanyId.Value)));

    private async Task ValidateAsync(PlanningDriverPairRequestDto request, Guid? currentId, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (request.FirstDriverId == Guid.Empty || request.SecondDriverId == Guid.Empty) errors.Add("Wybierz obu kierowców.");
        if (request.FirstDriverId == request.SecondDriverId) errors.Add("Para musi składać się z dwóch różnych kierowców.");
        var ids = new[] { request.FirstDriverId, request.SecondDriverId }.Distinct().ToList();
        if (await dbContext.Drivers.CountAsync(x => x.CompanyId == currentUser.CompanyId && ids.Contains(x.Id)
            && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId.Value), cancellationToken) != ids.Count) errors.Add("Kierowca nie należy do bieżącej firmy.");
        var duplicate = await dbContext.PlanningDriverPairs.AnyAsync(x => x.CompanyId == currentUser.CompanyId && x.Id != currentId
            && ((x.FirstDriverId == request.FirstDriverId && x.SecondDriverId == request.SecondDriverId) || (x.FirstDriverId == request.SecondDriverId && x.SecondDriverId == request.FirstDriverId))
            && x.IsNightDutyPair == request.IsNightDutyPair, cancellationToken);
        if (duplicate) errors.Add("Taka para kierowców już istnieje.");
        if ((request.Notes?.Trim().Length ?? 0) > 1000) errors.Add("Notatka jest za długa.");
        if (errors.Count > 0) throw new PlanningDutyValidationException(errors);
    }

    private static PlanningDriverPairDto Map(PlanningDriverPair x) => new()
    {
        Id = x.Id, FirstDriverId = x.FirstDriverId, FirstDriverName = Name(x.FirstDriver),
        SecondDriverId = x.SecondDriverId, SecondDriverName = Name(x.SecondDriver),
        IsNightDutyPair = x.IsNightDutyPair, PreventSameShift = x.PreventSameShift, IsActive = x.IsActive, Notes = x.Notes
    };
    private static string Name(Driver x) => string.IsNullOrWhiteSpace($"{x.LastName} {x.FirstName}".Trim()) ? x.CardNumber : $"{x.LastName} {x.FirstName}".Trim();
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
