using DriverTime.Application.Authentication.DTOs;

namespace DriverTime.Application.Drivers.DTOs;

public class DriverMobileAppInviteDto
{
    public Guid DriverId { get; set; }

    public string DriverFullName { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string InviteLink { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
}

public class MobileAppActivationRequestDto
{
    public string Token { get; set; } = string.Empty;
}

public class MobileAppActivationDto
{
    public string ApiBaseUrl { get; set; } = string.Empty;

    public AuthResponseDto Auth { get; set; } = new();

    public DriverDto Driver { get; set; } = new();
}
