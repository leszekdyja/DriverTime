using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningScheduleService
{
    Task<List<PlanningScheduleListItemDto>> GetSchedulesAsync(CancellationToken cancellationToken = default);

    Task<PlanningScheduleDto?> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlanningScheduleDto> CreateScheduleAsync(PlanningScheduleCreateRequestDto request, CancellationToken cancellationToken = default);

    Task<PlanningScheduleDto?> UpdateScheduleAsync(Guid id, PlanningScheduleUpdateRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlanningAssignmentDto?> UpsertAssignmentAsync(Guid scheduleId, PlanningAssignmentUpsertRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAssignmentAsync(Guid scheduleId, Guid assignmentId, CancellationToken cancellationToken = default);
}
