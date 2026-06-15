namespace DriverTime.Application.Interfaces;

public interface IJwtSettings
{
    string Issuer { get; }

    string Audience { get; }

    string Secret { get; }
}
