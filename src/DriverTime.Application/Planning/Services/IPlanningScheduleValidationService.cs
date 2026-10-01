using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningScheduleValidationService
{
    Task<PlanningScheduleValidationDto?> ValidateScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);
}
