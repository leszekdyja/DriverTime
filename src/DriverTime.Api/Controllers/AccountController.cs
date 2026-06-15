using DriverTime.Application.Account.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet("profile")]
    public async Task<ActionResult<AccountProfileDto>> GetProfile(
        CancellationToken cancellationToken)
    {
        var profile = await _accountService.GetProfileAsync(cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult<AccountProfileDto>> UpdateProfile(
        UpdateAccountProfileDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _accountService.UpdateProfileAsync(request, cancellationToken);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordDto request,
        CancellationToken cancellationToken)
    {
        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new { message = "Nowe haslo musi byc inne niz aktualne." });
        }

        try
        {
            var updated = await _accountService.ChangePasswordAsync(request, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
