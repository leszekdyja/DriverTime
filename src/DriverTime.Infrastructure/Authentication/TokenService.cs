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
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "HS256",
            typ = "JWT"
        }));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = $"{user.FirstName} {user.LastName}".Trim(),
            ["role"] = user.Role.Name,
            ["company_id"] = user.CompanyId.ToString(),
            ["iss"] = _settings.Issuer,
            ["aud"] = _settings.Audience,
            ["iat"] = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
            ["exp"] = new DateTimeOffset(expiresAtUtc).ToUnixTimeSeconds()
        }));
        var unsignedToken = $"{header}.{payload}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_settings.Secret),
            Encoding.UTF8.GetBytes(unsignedToken));

        return new AuthResponseDto
        {
            Token = $"{unsignedToken}.{Base64UrlEncode(signature)}",
            ExpiresAtUtc = expiresAtUtc,
            User = MapUser(user)
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
