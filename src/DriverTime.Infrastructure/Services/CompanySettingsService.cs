using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class CompanySettingsService : ICompanySettingsService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public CompanySettingsService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public Task<CompanySettingsDto?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.Id == _currentUser.CompanyId)
            .Select(x => new CompanySettingsDto
            {
                Name = x.Name,
                VatNumber = x.VatNumber,
                Address = x.Address,
                Email = x.Email,
                Phone = x.Phone
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CompanySettingsDto?> UpdateAsync(
        UpdateCompanySettingsDto settings,
        CancellationToken cancellationToken = default)
    {
        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(
                x => x.Id == _currentUser.CompanyId,
                cancellationToken);

        if (company is null)
        {
            return null;
        }

        company.Name = settings.Name.Trim();
        company.VatNumber = settings.VatNumber?.Trim() ?? string.Empty;
        company.Address = settings.Address?.Trim() ?? string.Empty;
        company.Email = settings.Email?.Trim() ?? string.Empty;
        company.Phone = settings.Phone?.Trim() ?? string.Empty;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CompanySettingsDto
        {
            Name = company.Name,
            VatNumber = company.VatNumber,
            Address = company.Address,
            Email = company.Email,
            Phone = company.Phone
        };
    }
}
