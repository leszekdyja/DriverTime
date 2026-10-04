using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Authentication;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Authentication;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class OperatingCompanyService : IOperatingCompanyService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;

    public OperatingCompanyService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IPasswordHasher? passwordHasher = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher ?? new PasswordHasher();
    }

    public Task<List<OperatingCompanyDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _dbContext.OperatingCompanies
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId
                && (!_currentUser.OperatingCompanyId.HasValue || x.Id == _currentUser.OperatingCompanyId.Value))
            .OrderBy(x => x.Name)
            .Select(x => new OperatingCompanyDto
            {
                Id = x.Id,
                Name = x.Name,
                TaxNumber = x.TaxNumber,
                Active = x.Active,
                DriversCount = x.Drivers.Count,
                AccountEmail = x.Users.OrderBy(u => u.CreatedAt).Select(u => u.Email).FirstOrDefault(),
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

    public async Task<OperatingCompanyDto> CreateAsync(CreateOperatingCompanyDto request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageCompanies();
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

        if (request.CreateLoginAccount)
        {
            var email = request.AccountEmail.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                throw new ArgumentException("Podaj prawidłowy adres e-mail konta firmy.");
            if (request.AccountPassword.Length < 8)
                throw new ArgumentException("Hasło konta firmy musi mieć co najmniej 8 znaków.");
            if (await _dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken))
                throw new InvalidOperationException("Konto z tym adresem e-mail już istnieje.");

            var dispatcherRole = await _dbContext.Roles
                .SingleAsync(x => x.Name == RoleNames.Dispatcher, cancellationToken);
            company.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                CompanyId = _currentUser.CompanyId,
                OperatingCompanyId = company.Id,
                Email = email,
                FirstName = request.AccountFirstName.Trim(),
                LastName = request.AccountLastName.Trim(),
                PasswordHash = _passwordHasher.Hash(request.AccountPassword),
                RoleId = dispatcherRole.Id,
                Active = true
            });
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(company);
    }

    public async Task<OperatingCompanyDto?> UpdateAsync(Guid id, SaveOperatingCompanyDto request, CancellationToken cancellationToken = default)
    {
        EnsureCanManageCompanies();
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
        EnsureCanManageCompanies();
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

    private void EnsureCanManageCompanies()
    {
        if (_currentUser.OperatingCompanyId.HasValue)
            throw new InvalidOperationException("Konto firmy nie może zarządzać innymi firmami.");
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
        AccountEmail = company.Users.OrderBy(x => x.CreatedAt).Select(x => x.Email).FirstOrDefault(),
        CreatedAtUtc = company.CreatedAtUtc
    };
}
