using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningDutyBlockService(DriverTimeDbContext dbContext, ICurrentUserService currentUser) : IPlanningDutyBlockService
{
    public Task<List<PlanningDutyBlockDto>> GetAsync(CancellationToken cancellationToken = default) =>
        Query().OrderBy(x => x.FirstDuty.DutyNumber).Select(x => Map(x)).ToListAsync(cancellationToken);

    public async Task<PlanningDutyBlockDto> CreateAsync(PlanningDutyBlockRequestDto request, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        if (request.FirstDutyId == Guid.Empty || request.SecondDutyId == Guid.Empty) errors.Add("Wybierz obie służby.");
        if (request.FirstDutyId == request.SecondDutyId) errors.Add("Blokada musi łączyć dwie różne służby.");
        var ids = new[] { request.FirstDutyId, request.SecondDutyId }.Distinct().ToList();
        if (await dbContext.PlanningDuties.CountAsync(x => x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId.Value)
            && ids.Contains(x.Id), cancellationToken) != ids.Count) errors.Add("Służba nie należy do bieżącej firmy.");
        if (await dbContext.PlanningDutyBlocks.AnyAsync(x => x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId.Value)
            && (x.FirstDutyId == request.FirstDutyId || x.SecondDutyId == request.FirstDutyId
                || x.FirstDutyId == request.SecondDutyId || x.SecondDutyId == request.SecondDutyId), cancellationToken))
        {
            errors.Add("Jedna z wybranych służb należy już do innej blokady.");
        }
        if (errors.Count > 0) throw new PlanningDutyValidationException(errors);
        var entity = new PlanningDutyBlock { Id = Guid.NewGuid(), CompanyId = currentUser.CompanyId, OperatingCompanyId = currentUser.OperatingCompanyId, FirstDutyId = request.FirstDutyId, SecondDutyId = request.SecondDutyId, RequiredVehicleType = Normalize(request.RequiredVehicleType), Notes = Normalize(request.Notes), IsActive = request.IsActive };
        dbContext.PlanningDutyBlocks.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await Query().Where(x => x.Id == entity.Id).Select(x => Map(x)).SingleAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PlanningDutyBlocks.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId.Value), cancellationToken);
        if (entity is null) return false;
        dbContext.PlanningDutyBlocks.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<PlanningDutyBlock> Query() => dbContext.PlanningDutyBlocks.AsNoTracking().Include(x => x.FirstDuty).Include(x => x.SecondDuty).Where(x => x.CompanyId == currentUser.CompanyId
        && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId.Value));
    private static PlanningDutyBlockDto Map(PlanningDutyBlock x) => new() { Id = x.Id, FirstDutyId = x.FirstDutyId, FirstDutyNumber = x.FirstDuty.DutyNumber, SecondDutyId = x.SecondDutyId, SecondDutyNumber = x.SecondDuty.DutyNumber, RequiredVehicleType = x.RequiredVehicleType, Notes = x.Notes, IsActive = x.IsActive };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
