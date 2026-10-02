using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningDutyBlockService
{
    Task<List<PlanningDutyBlockDto>> GetAsync(CancellationToken cancellationToken = default);
    Task<PlanningDutyBlockDto> CreateAsync(PlanningDutyBlockRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
