using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class OperatingCompanyService : IOperatingCompanyService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public OperatingCompanyService(DriverTimeDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public Task<List<OperatingCompanyDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _dbContext.OperatingCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .OrderBy(x => x.Name)
            .Select(x => new OperatingCompanyDto
            {
                Id = x.Id,
                Name = x.Name,
                TaxNumber = x.TaxNumber,
                Active = x.Active,
                DriversCount = x.Drivers.Count,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

    public async Task<OperatingCompanyDto> CreateAsync(SaveOperatingCompanyDto request, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(request.Name);
        await EnsureUniqueNameAsync(name, null, cancellationToken);
        var company = new OperatingCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            Name = name,
            TaxNumber = request.TaxNumber.Trim(),
            Active = request.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.OperatingCompanies.Add(company);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(company);
    }

    public async Task<OperatingCompanyDto?> UpdateAsync(Guid id, SaveOperatingCompanyDto request, CancellationToken cancellationToken = default)
    {
        var company = await _dbContext.OperatingCompanies
            .Include(x => x.Drivers)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId, cancellationToken);
        if (company is null) return null;

        var name = NormalizeName(request.Name);
        await EnsureUniqueNameAsync(name, id, cancellationToken);
        company.Name = name;
        company.TaxNumber = request.TaxNumber.Trim();
        company.Active = request.Active;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(company);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _dbContext.OperatingCompanies
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId, cancellationToken);
        if (company is null) return false;
        _dbContext.OperatingCompanies.Remove(company);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? excludedId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.OperatingCompanies.AnyAsync(
            x => x.CompanyId == _currentUser.CompanyId && x.Name.ToLower() == name.ToLower() && x.Id != excludedId,
            cancellationToken);
        if (exists) throw new InvalidOperationException("Firma o tej nazwie już istnieje.");
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Nazwa firmy jest wymagana.");
        return normalized;
    }

    private static OperatingCompanyDto Map(OperatingCompany company) => new()
    {
        Id = company.Id,
        Name = company.Name,
        TaxNumber = company.TaxNumber,
        Active = company.Active,
        DriversCount = company.Drivers.Count,
        CreatedAtUtc = company.CreatedAtUtc
    };
}
