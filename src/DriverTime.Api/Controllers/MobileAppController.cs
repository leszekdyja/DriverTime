using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/mobile")]
public class MobileAppController : ControllerBase
{
    private readonly IDriverMobileAppInviteService _mobileAppInviteService;

    public MobileAppController(IDriverMobileAppInviteService mobileAppInviteService)
    {
        _mobileAppInviteService = mobileAppInviteService;
    }

    [AllowAnonymous]
    [HttpPost("activate")]
    public async Task<ActionResult<MobileAppActivationDto>> Activate(
        [FromBody] MobileAppActivationRequestDto request,
        CancellationToken cancellationToken)
    {
        var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var activation = await _mobileAppInviteService.ActivateAsync(
            request.Token,
            apiBaseUrl,
            cancellationToken);

        return activation is null
            ? BadRequest(new { message = "Link konfiguracji aplikacji jest niewazny albo wygasl." })
            : Ok(activation);
    }
}
