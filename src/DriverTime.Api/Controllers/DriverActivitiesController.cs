using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/driver-activities")]
public class DriverActivitiesController : ControllerBase
{
    private readonly IDriverActivityService _driverActivityService;

    public DriverActivitiesController(
        IDriverActivityService driverActivityService)
    {
        _driverActivityService = driverActivityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivities(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? driverCardNumber)
    {
        var result = await _driverActivityService
            .GetActivitiesAsync(from, to, driverCardNumber);

        return Ok(result);
    }
}
