using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningDriverPairService
{
    Task<List<PlanningDriverPairDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<PlanningDriverPairDto> CreateAsync(PlanningDriverPairRequestDto request, CancellationToken cancellationToken = default);
    Task<PlanningDriverPairDto?> UpdateAsync(Guid id, PlanningDriverPairRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
