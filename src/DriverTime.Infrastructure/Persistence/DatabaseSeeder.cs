using System.Security.Cryptography;
using System.Text;
using DriverTime.Application.Authentication;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private const string DefaultDemoEmail = "demo@drivertime.app";
    private const string DefaultDemoPassword = "DriverTimeDemo123!";

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

    public async Task SeedAsync(
        bool seedDemoData,
        CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        var adminCompany = await SeedAdministratorAsync(cancellationToken);
        await AssignLegacyDataAsync(adminCompany.Id, cancellationToken);

        if (seedDemoData)
        {
            await SeedDemoDataAsync(cancellationToken);
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in RoleNames.All)
        {
            if (!await _dbContext.Roles.AnyAsync(x => x.Name == roleName, cancellationToken))
            {
                _dbContext.Roles.Add(new Role { Id = Guid.NewGuid(), Name = roleName });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Company> SeedAdministratorAsync(CancellationToken cancellationToken)
    {
        var adminEmail = NormalizeEmail(
            _configuration["SeedAdmin:Email"] ?? "admin@drivertime.local");
        var companyName = _configuration["SeedAdmin:CompanyName"] ?? "DriverTime Local";
        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(x => x.Name == companyName, cancellationToken);

        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = companyName };
            _dbContext.Companies.Add(company);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (await _dbContext.Users.AnyAsync(x => x.Email == adminEmail, cancellationToken))
        {
            return company;
        }

        var adminRole = await GetAdminRoleAsync(cancellationToken);
        var configuredPassword = _configuration["SeedAdmin:Password"];
        var adminPassword = string.IsNullOrWhiteSpace(configuredPassword)
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            : configuredPassword;

        _dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            RoleId = adminRole.Id,
            Email = adminEmail,
            FirstName = "System",
            LastName = "Administrator",
            PasswordHash = _passwordHasher.Hash(adminPassword)
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            _logger.LogWarning(
                "Seed admin created. Email: {Email}. Generated password: {Password}. Configure SeedAdmin__Password to provide your own password.",
                adminEmail,
                adminPassword);
        }

        return company;
    }

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        var demoEmail = NormalizeEmail(
            _configuration["DemoData:Email"] ?? DefaultDemoEmail);
        var demoPassword = _configuration["DemoData:Password"] ?? DefaultDemoPassword;
        var companyName = _configuration["DemoData:CompanyName"] ?? "Northline Logistics";
        var company = await _dbContext.Companies
            .FirstOrDefaultAsync(x => x.Name == companyName, cancellationToken);

        if (company is null)
        {
            company = new Company
            {
                Id = Guid.NewGuid(),
                Name = companyName,
                VatNumber = "PL5213987654",
                Address = "ul. Logistyczna 12, 00-950 Warszawa",
                Email = "biuro@northline.demo",
                Phone = "+48 22 100 20 30"
            };
            _dbContext.Companies.Add(company);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await _dbContext.Users.AnyAsync(x => x.Email == demoEmail, cancellationToken))
        {
            var adminRole = await GetAdminRoleAsync(cancellationToken);
            _dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                RoleId = adminRole.Id,
                Email = demoEmail,
                FirstName = "Anna",
                LastName = "Demo",
                PasswordHash = _passwordHasher.Hash(demoPassword)
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var driverSeeds = new[]
        {
            new DemoDriver("Marek", "Kowalski", "PL-MK-78451236", "PL", "WZ 8421K", 1, false),
            new DemoDriver("Piotr", "Nowak", "PL-PN-36581472", "PL", "PO 7NL21", 3, true),
            new DemoDriver("Ewa", "Wisniewska", "PL-EW-95142683", "PL", "GD 4RT90", 8, false)
        };

        foreach (var seed in driverSeeds)
        {
            await SeedDemoDriverAsync(company, seed, cancellationToken);
        }

        _logger.LogInformation(
            "Demo data is ready. Login: {Email}. Demo seeding is enabled only for Development or explicit DemoData__Enabled configuration.",
            demoEmail);
    }

    private async Task SeedDemoDriverAsync(
        Company company,
        DemoDriver seed,
        CancellationToken cancellationToken)
    {
        var driver = await _dbContext.Drivers.FirstOrDefaultAsync(
            x => x.CompanyId == company.Id && x.CardNumber == seed.CardNumber,
            cancellationToken);

        if (driver is null)
        {
            driver = new Driver
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                FirstName = seed.FirstName,
                LastName = seed.LastName,
                CardNumber = seed.CardNumber,
                CardIssuingCountry = seed.CountryCode,
                CardExpiryDate = DateTime.UtcNow.Date.AddYears(3),
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-4)
            };
            _dbContext.Drivers.Add(driver);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var fileHash = CreateHash($"drivertime-demo-{seed.CardNumber}");
        if (await _dbContext.DddFiles.AnyAsync(
                x => x.CompanyId == company.Id && x.FileHash == fileHash,
                cancellationToken))
        {
            return;
        }

        var uploadedAt = DateTime.UtcNow.Date.AddDays(-seed.DaysAgo).AddHours(9);
        var dddFile = new DddFile
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            DriverId = driver.Id,
            FileName = $"{seed.LastName.ToUpperInvariant()}_{uploadedAt:yyyyMMdd}.ddd",
            FileHash = fileHash,
            DriverCreatedDuringImport = true,
            UploadedAtUtc = uploadedAt,
            DriverCardNumber = seed.CardNumber,
            DriverFirstName = seed.FirstName,
            DriverLastName = seed.LastName
        };

        dddFile.VehicleUses.Add(new VehicleUse
        {
            Id = Guid.NewGuid(),
            DddFileId = dddFile.Id,
            RegistrationNumber = seed.VehicleRegistration,
            StartUtc = uploadedAt.AddDays(-6),
            EndUtc = uploadedAt
        });
        dddFile.CountryEntries.Add(new CountryEntry
        {
            Id = Guid.NewGuid(),
            DddFileId = dddFile.Id,
            CountryCode = seed.CountryCode,
            EntryTimeUtc = uploadedAt.AddDays(-6).AddHours(6)
        });

        for (var dayOffset = 6; dayOffset >= 0; dayOffset--)
        {
            AddDemoDay(
                dddFile,
                uploadedAt.Date.AddDays(-dayOffset),
                seed.IncludeViolation && dayOffset == 1);
        }

        _dbContext.DddFiles.Add(dddFile);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AddDemoDay(DddFile dddFile, DateTime day, bool includeViolation)
    {
        AddActivity(dddFile, day, 0, 6, "REST");
        AddActivity(dddFile, day, 6, 6.5, "WORK");

        if (includeViolation)
        {
            AddActivity(dddFile, day, 6.5, 11.5, "DRIVING");
            AddActivity(dddFile, day, 11.5, 12, "REST");
            AddActivity(dddFile, day, 12, 17.5, "DRIVING");
            AddActivity(dddFile, day, 17.5, 18, "WORK");
            AddActivity(dddFile, day, 18, 24, "REST");
            return;
        }

        AddActivity(dddFile, day, 6.5, 10.5, "DRIVING");
        AddActivity(dddFile, day, 10.5, 11.25, "REST");
        AddActivity(dddFile, day, 11.25, 15.75, "DRIVING");
        AddActivity(dddFile, day, 15.75, 16.5, "WORK");
        AddActivity(dddFile, day, 16.5, 17, "AVAILABILITY");
        AddActivity(dddFile, day, 17, 24, "REST");
    }

    private static void AddActivity(
        DddFile dddFile,
        DateTime day,
        double startHour,
        double endHour,
        string activityType)
    {
        dddFile.DriverActivities.Add(new DriverActivity
        {
            Id = Guid.NewGuid(),
            DddFileId = dddFile.Id,
            StartUtc = DateTime.SpecifyKind(day.AddHours(startHour), DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(day.AddHours(endHour), DateTimeKind.Utc),
            ActivityType = activityType
        });
    }

    private async Task<Role> GetAdminRoleAsync(CancellationToken cancellationToken) =>
        await _dbContext.Roles.SingleAsync(x => x.Name == RoleNames.Admin, cancellationToken);

    private async Task AssignLegacyDataAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await _dbContext.DddFiles
            .Where(x => x.CompanyId == Guid.Empty)
            .ExecuteUpdateAsync(
                x => x.SetProperty(file => file.CompanyId, companyId),
                cancellationToken);
        await _dbContext.Drivers
            .Where(x => x.CompanyId == Guid.Empty)
            .ExecuteUpdateAsync(
                x => x.SetProperty(driver => driver.CompanyId, companyId),
                cancellationToken);
    }

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static string CreateHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record DemoDriver(
        string FirstName,
        string LastName,
        string CardNumber,
        string CountryCode,
        string VehicleRegistration,
        int DaysAgo,
        bool IncludeViolation);
}
