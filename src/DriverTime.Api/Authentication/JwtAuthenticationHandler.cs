using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverTime.Api.Authentication;

public class JwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    private readonly IJwtSettings _settings;
    private readonly DriverTimeDbContext _dbContext;

    public JwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IJwtSettings settings,
        DriverTimeDbContext dbContext)
        : base(options, logger, encoder)
    {
        _settings = settings;
        _dbContext = dbContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var principal = ValidateToken(authorization["Bearer ".Length..].Trim());
            await RefreshUserScopeAsync(principal, Context.RequestAborted);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception exception) when (
            exception is FormatException
            or JsonException
            or CryptographicException
            or InvalidOperationException)
        {
            return AuthenticateResult.Fail("Invalid or expired JWT token.");
        }
    }

    private async Task RefreshUserScopeAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var identity = principal.Identity as ClaimsIdentity
            ?? throw new InvalidOperationException("JWT identity is unavailable.");
        if (identity.FindFirst("token_type")?.Value == "mobile_driver")
        {
            return;
        }

        if (!Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            throw new InvalidOperationException("JWT user identifier is invalid.");
        }

        var userScope = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.Active)
            .Select(user => new { user.CompanyId, user.OperatingCompanyId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("JWT user is inactive or no longer exists.");

        ReplaceClaim(identity, "company_id", userScope.CompanyId.ToString());
        RemoveClaims(identity, "operating_company_id");
        if (userScope.OperatingCompanyId.HasValue)
        {
            identity.AddClaim(new Claim(
                "operating_company_id",
                userScope.OperatingCompanyId.Value.ToString()));
        }
    }

    private static void ReplaceClaim(ClaimsIdentity identity, string claimType, string value)
    {
        RemoveClaims(identity, claimType);
        identity.AddClaim(new Claim(claimType, value));
    }

    private static void RemoveClaims(ClaimsIdentity identity, string claimType)
    {
        foreach (var claim in identity.FindAll(claimType).ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        var parts = token.Split('.');

        if (parts.Length != 3)
        {
            throw new FormatException("Invalid JWT format.");
        }

        var unsignedToken = $"{parts[0]}.{parts[1]}";
        var expectedSignature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_settings.Secret),
            Encoding.UTF8.GetBytes(unsignedToken));
        var actualSignature = Base64UrlDecode(parts[2]);

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
        {
            throw new CryptographicException("Invalid JWT signature.");
        }

        using var document = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        var payload = document.RootElement;

        if (payload.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            || payload.GetProperty("iss").GetString() != _settings.Issuer
            || payload.GetProperty("aud").GetString() != _settings.Audience)
        {
            throw new InvalidOperationException("JWT validation failed.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, GetString(payload, "sub")),
            new Claim(ClaimTypes.Email, GetString(payload, "email")),
            new Claim(ClaimTypes.Name, GetString(payload, "name")),
            new Claim(ClaimTypes.Role, GetString(payload, "role")),
            new Claim("company_id", GetString(payload, "company_id")),
            new Claim("token_type", payload.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() ?? "user" : "user")
        };

        if (payload.TryGetProperty("driver_id", out var driverId))
        {
            claims.Add(new Claim("driver_id", driverId.GetString() ?? string.Empty));
        }

        if (payload.TryGetProperty("operating_company_id", out var operatingCompanyId))
        {
            var value = operatingCompanyId.GetString();
            if (Guid.TryParse(value, out _))
            {
                claims.Add(new Claim("operating_company_id", value!));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
    }

    private static string GetString(JsonElement payload, string propertyName) =>
        payload.GetProperty(propertyName).GetString() ?? string.Empty;

    private static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');

        return Convert.FromBase64String(normalized);
    }
}
