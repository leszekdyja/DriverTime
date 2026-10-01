using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningManualAssignmentService
{
    Task<PlanningAssignmentDto> CreateManualAsync(PlanningManualAssignmentRequestDto request, CancellationToken cancellationToken = default);

    Task<PlanningAssignmentDto?> UpdateAsync(Guid id, PlanningManualAssignmentRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
