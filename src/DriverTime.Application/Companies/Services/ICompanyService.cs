using DriverTime.Application.Companies.DTOs;

namespace DriverTime.Application.Companies.Services;

public interface ICompanyService
{
    Task<IEnumerable<CompanyDto>> GetAllAsync();

    Task<CompanyDto> CreateAsync(CreateCompanyDto dto);
}
