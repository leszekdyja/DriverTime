using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningDriverAvailabilityService
{
    Task<List<PlanningDriverAvailabilityDto>> GetAsync(DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken = default);

    Task<PlanningDriverAvailabilityDto> CreateAsync(PlanningDriverAvailabilityCreateRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
