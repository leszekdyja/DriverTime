using System.Security.Cryptography;
using DriverTime.Application.Authentication;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        DriverTimeDbContext dbContext,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (!await _dbContext.Roles.AnyAsync(x => x.Name == roleName, cancellationToken))
            {
                _dbContext.Roles.Add(new Role { Id = Guid.NewGuid(), Name = roleName });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var adminEmail = (_configuration["SeedAdmin:Email"] ?? "admin@drivertime.local")
            .Trim()
            .ToLowerInvariant();

        if (await _dbContext.Users.AnyAsync(x => x.Email == adminEmail, cancellationToken))
        {
            return;
        }

        var companyName = _configuration["SeedAdmin:CompanyName"] ?? "DriverTime Demo";
        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(x => x.Name == companyName, cancellationToken)
            ?? new Company { Id = Guid.NewGuid(), Name = companyName };
        var adminRole = await _dbContext.Roles
            .SingleAsync(x => x.Name == RoleNames.Admin, cancellationToken);
        var configuredPassword = _configuration["SeedAdmin:Password"];
        var adminPassword = string.IsNullOrWhiteSpace(configuredPassword)
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            : configuredPassword;
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Company = company,
            Role = adminRole,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            PasswordHash = _passwordHasher.Hash(adminPassword)
        };

        _dbContext.Users.Add(admin);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            _logger.LogWarning(
                "Seed admin created. Email: {Email}. Generated password: {Password}. Configure SeedAdmin__Password to provide your own password.",
                adminEmail,
                adminPassword);
        }

        await _dbContext.DddFiles
            .Where(x => x.CompanyId == Guid.Empty)
            .ExecuteUpdateAsync(x => x.SetProperty(file => file.CompanyId, company.Id), cancellationToken);
        await _dbContext.Drivers
            .Where(x => x.CompanyId == Guid.Empty)
            .ExecuteUpdateAsync(x => x.SetProperty(driver => driver.CompanyId, company.Id), cancellationToken);
    }
}
