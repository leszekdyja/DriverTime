using DriverTime.Application.Drivers.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverService
{
    Task<List<DriverDto>> GetAllAsync();

    Task<DriverDetailsDto?> GetByIdAsync(Guid id);

    Task<DriverDto> CreateAsync(CreateDriverDto dto);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
