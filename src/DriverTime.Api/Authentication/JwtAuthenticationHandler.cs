using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DriverTime.Api.Authentication;

public class JwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    private readonly IJwtSettings _settings;

    public JwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IJwtSettings settings)
        : base(options, logger, encoder)
    {
        _settings = settings;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            var principal = ValidateToken(authorization["Bearer ".Length..].Trim());
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception exception) when (
            exception is FormatException
            or JsonException
            or CryptographicException
            or InvalidOperationException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired JWT token."));
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

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, GetString(payload, "sub")),
            new Claim(ClaimTypes.Email, GetString(payload, "email")),
            new Claim(ClaimTypes.Name, GetString(payload, "name")),
            new Claim(ClaimTypes.Role, GetString(payload, "role")),
            new Claim("company_id", GetString(payload, "company_id"))
        };

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
