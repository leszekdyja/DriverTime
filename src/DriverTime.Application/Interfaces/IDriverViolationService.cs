using DriverTime.Application.Violations.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverViolationService
{
    Task<IReadOnlyList<DriverViolationDto>> GetViolationsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverViolationDto>> GetViolationsForDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);
}
