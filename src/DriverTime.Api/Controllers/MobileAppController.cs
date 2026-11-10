using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/mobile")]
public class MobileAppController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("activate")]
    public ActionResult<MobileAppActivationDto> Activate(
        [FromBody] MobileAppActivationRequestDto request,
        CancellationToken cancellationToken)
    {
        return StatusCode(StatusCodes.Status410Gone, new { message = "Integracja z aplikacją mobilną została wyłączona." });
    }
}
