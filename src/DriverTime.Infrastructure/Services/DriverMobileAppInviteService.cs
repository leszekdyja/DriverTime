using System.Security.Cryptography;
using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverMobileAppInviteService : IDriverMobileAppInviteService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITokenService _tokenService;

    public DriverMobileAppInviteService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        ITokenService tokenService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tokenService = tokenService;
    }

    public async Task<DriverMobileAppInviteDto?> CreateInviteAsync(
        Guid driverId,
        string apiBaseUrl,
        string inviteLinkBase,
        CancellationToken cancellationToken = default)
    {
        var driver = await _dbContext.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId
                && (!_currentUser.OperatingCompanyId.HasValue
                    || x.OperatingCompanyId == _currentUser.OperatingCompanyId.Value), cancellationToken);

        if (driver is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        await _dbContext.MobileAppInvites
            .Where(x =>
                x.CompanyId == _currentUser.CompanyId
                && x.DriverId == driverId
                && x.UsedAtUtc == null
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.RevokedAtUtc, now),
                cancellationToken);

        var token = CreateToken();
        var invite = new MobileAppInvite
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            DriverId = driverId,
            TokenHash = HashToken(token),
            ExpiresAtUtc = now.AddDays(30),
            CreatedAtUtc = now
        };

        _dbContext.MobileAppInvites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var normalizedApiBaseUrl = apiBaseUrl.TrimEnd('/');
        var inviteLink = $"{inviteLinkBase}?apiUrl={Uri.EscapeDataString(normalizedApiBaseUrl)}&token={Uri.EscapeDataString(token)}";

        return new DriverMobileAppInviteDto
        {
            DriverId = driver.Id,
            DriverFullName = FormatDriverName(driver),
            Token = token,
            ApiBaseUrl = normalizedApiBaseUrl,
            InviteLink = inviteLink,
            ExpiresAtUtc = invite.ExpiresAtUtc
        };
    }

    public async Task<MobileAppActivationDto?> ActivateAsync(
        string token,
        string apiBaseUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(token.Trim());
        var invite = await _dbContext.MobileAppInvites
            .Include(x => x.Company)
            .Include(x => x.Driver)
            .FirstOrDefaultAsync(x =>
                x.TokenHash == tokenHash
                && x.UsedAtUtc == null
                && x.RevokedAtUtc == null
                && x.ExpiresAtUtc > now,
                cancellationToken);

        if (invite is null)
        {
            return null;
        }

        invite.UsedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new MobileAppActivationDto
        {
            ApiBaseUrl = apiBaseUrl.TrimEnd('/'),
            Auth = _tokenService.CreateMobileDriverToken(invite.Company, invite.Driver),
            Driver = MapDriver(invite.Driver)
        };
    }

    private static DriverDto MapDriver(Driver driver) =>
        new()
        {
            Id = driver.Id,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            CardNumber = driver.CardNumber,
            CardExpiryDate = driver.CardExpiryDate,
            CardIssuingCountry = driver.CardIssuingCountry,
            IncludeInPlanning = driver.IncludeInPlanning,
            PlanningNoNightDuty = driver.PlanningNoNightDuty,
            PlanningNoWeekends = driver.PlanningNoWeekends,
            PlanningNoSaturdays = driver.PlanningNoSaturdays,
            PlanningNoHolidays = driver.PlanningNoHolidays,
            PlanningNoDaysOff = driver.PlanningNoDaysOff,
            CreatedAtUtc = driver.CreatedAtUtc
        };

    private static string FormatDriverName(Driver driver) =>
        $"{driver.LastName} {driver.FirstName}".Trim();

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
