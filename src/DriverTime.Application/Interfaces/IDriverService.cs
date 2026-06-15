using DriverTime.Application.Drivers.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverService
{
    Task<List<DriverDto>> GetAllAsync();

    Task<DriverDto?> GetByIdAsync(Guid id);

    Task<DriverDto> CreateAsync(CreateDriverDto dto);
}