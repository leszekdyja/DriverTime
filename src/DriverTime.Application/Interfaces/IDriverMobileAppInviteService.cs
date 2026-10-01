using DriverTime.Application.Drivers.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverMobileAppInviteService
{
    Task<DriverMobileAppInviteDto?> CreateInviteAsync(
        Guid driverId,
        string apiBaseUrl,
        string inviteLinkBase,
        CancellationToken cancellationToken = default);

    Task<MobileAppActivationDto?> ActivateAsync(
        string token,
        string apiBaseUrl,
        CancellationToken cancellationToken = default);
}
