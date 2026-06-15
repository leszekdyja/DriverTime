using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/driver-violations")]
public class DriverViolationsController : ControllerBase
{
    private readonly IDriverViolationService _driverViolationService;

    public DriverViolationsController(
        IDriverViolationService driverViolationService)
    {
        _driverViolationService = driverViolationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DriverViolationDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var violations = await _driverViolationService.GetViolationsAsync(
            cancellationToken);

        return Ok(violations);
    }
}
