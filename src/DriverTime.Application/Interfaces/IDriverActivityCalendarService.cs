using DriverTime.Application.Drivers.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverActivityCalendarService
{
    Task<DriverActivityCalendarDto?> GetAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
