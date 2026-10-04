using DriverTime.Application.Companies.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IOperatingCompanyService
{
    Task<List<OperatingCompanyDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<OperatingCompanyDto> CreateAsync(CreateOperatingCompanyDto request, CancellationToken cancellationToken = default);
    Task<OperatingCompanyDto?> UpdateAsync(Guid id, SaveOperatingCompanyDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
