using DriverTime.Application.Companies.DTOs;

namespace DriverTime.Application.Companies.Services;

public class CompanyService : ICompanyService
{
    private static readonly List<CompanyDto> Companies = new();

    public Task<IEnumerable<CompanyDto>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<CompanyDto>>(Companies);
    }

    public Task<CompanyDto> CreateAsync(CreateCompanyDto dto)
    {
        var company = new CompanyDto
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            VatNumber = dto.VatNumber,
            Address = dto.Address
        };

        Companies.Add(company);

        return Task.FromResult(company);
    }
}