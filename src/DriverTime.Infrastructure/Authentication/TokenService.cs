using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DriverTime.Application.Authentication.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Authentication;

public class TokenService : ITokenService
{
    private readonly IJwtSettings _settings;

    public TokenService(IJwtSettings settings)
    {
        _settings = settings;
    }

    public AuthResponseDto CreateToken(User user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddHours(8);
        var payload = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = $"{user.FirstName} {user.LastName}".Trim(),
            ["role"] = user.Role.Name,
            ["company_id"] = user.CompanyId.ToString(),
            ["token_type"] = "user"
        };

        return CreateToken(payload, expiresAtUtc, MapUser(user));
    }

    public AuthResponseDto CreateMobileDriverToken(Company company, Driver driver)
    {
        var expiresAtUtc = DateTime.UtcNow.AddDays(30);
        var fullName = $"{driver.FirstName} {driver.LastName}".Trim();
        var user = new CurrentUserDto
        {
            Id = driver.Id,
            CompanyId = company.Id,
            CompanyName = company.Name,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            Email = $"driver-{driver.Id:N}@mobile.drivertime.local",
            Role = "MobileDriver",
            DriverId = driver.Id
        };

        var payload = new Dictionary<string, object>
        {
            ["sub"] = driver.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = fullName,
            ["role"] = user.Role,
            ["company_id"] = company.Id.ToString(),
            ["driver_id"] = driver.Id.ToString(),
            ["token_type"] = "mobile_driver"
        };

        return CreateToken(payload, expiresAtUtc, user);
    }

    private AuthResponseDto CreateToken(
        Dictionary<string, object> payload,
        DateTime expiresAtUtc,
        CurrentUserDto user)
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "HS256",
            typ = "JWT"
        }));
        payload["iss"] = _settings.Issuer;
        payload["aud"] = _settings.Audience;
        payload["iat"] = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        payload["exp"] = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds();
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var unsignedToken = $"{header}.{encodedPayload}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_settings.Secret),
            Encoding.UTF8.GetBytes(unsignedToken));

        return new AuthResponseDto
        {
            Token = $"{unsignedToken}.{Base64UrlEncode(signature)}",
            ExpiresAtUtc = expiresAtUtc,
            User = user
        };
    }

    private static CurrentUserDto MapUser(User user)
    {
        return new CurrentUserDto
        {
            Id = user.Id,
            CompanyId = user.CompanyId,
            CompanyName = user.Company?.Name ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.Name
        };
    }

    private static string Base64UrlEncode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
