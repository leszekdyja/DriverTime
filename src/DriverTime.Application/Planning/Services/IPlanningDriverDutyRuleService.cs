using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningDriverDutyRuleService
{
    Task<List<PlanningDriverDutyRuleDto>> GetAsync(PlanningDriverDutyRuleFilterDto filter, CancellationToken cancellationToken = default);

    Task<PlanningDriverDutyRuleDto> CreateAsync(PlanningDriverDutyRuleRequestDto request, CancellationToken cancellationToken = default);

    Task<PlanningDriverDutyRuleDto?> UpdateAsync(Guid id, PlanningDriverDutyRuleRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
