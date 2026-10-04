using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverActivityService
{
    Task<List<DriverActivityDto>> GetActivitiesAsync(
        DateTime? from,
        DateTime? to,
        string? driverCardNumber,
        Guid? driverId = null,
        Guid? operatingCompanyId = null,
        IReadOnlyCollection<Guid>? driverIds = null);
}
