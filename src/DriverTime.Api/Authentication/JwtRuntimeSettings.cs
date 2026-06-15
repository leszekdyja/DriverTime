using System.Security.Cryptography;
using DriverTime.Application.Interfaces;

namespace DriverTime.Api.Authentication;

public class JwtRuntimeSettings : IJwtSettings
{
    public JwtRuntimeSettings(IConfiguration configuration)
    {
        Issuer = configuration["Jwt:Issuer"] ?? "DriverTime";
        Audience = configuration["Jwt:Audience"] ?? "DriverTime";
        Secret = configuration["Jwt:Secret"]
            ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }

    public string Issuer { get; }

    public string Audience { get; }

    public string Secret { get; }
}
