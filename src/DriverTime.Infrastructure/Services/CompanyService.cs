using DriverTime.Application.Common.Interfaces;
using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Companies.Services;
using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class CompanyService : ICompanyService
{
    private readonly IApplicationDbContext _context;

    public CompanyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CompanyDto>> GetAllAsync()
    {
        return await _context.Companies
            .Select(company => new CompanyDto
            {
                Id = company.Id,
                Name = company.Name,
                VatNumber = company.VatNumber,
                Address = company.Address
            })
            .ToListAsync();
    }

    public async Task<CompanyDto> CreateAsync(CreateCompanyDto dto)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            VatNumber = dto.VatNumber,
            Address = dto.Address
        };

        _context.Companies.Add(company);

        await _context.SaveChangesAsync();

        return new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            VatNumber = company.VatNumber,
            Address = company.Address
        };
    }
}
