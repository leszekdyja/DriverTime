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
        [FromQuery] string? driverCardNumber,
        [FromQuery] Guid? driverId,
        [FromQuery] Guid? operatingCompanyId,
        [FromQuery] Guid[]? driverIds,
        [FromQuery] DateOnly? localFrom,
        [FromQuery] DateOnly? localTo)
    {
        if (localFrom.HasValue)
        {
            from = ToUtc(localFrom.Value);
        }

        if (localTo.HasValue)
        {
            to = ToUtc(localTo.Value.AddDays(1));
        }

        var result = await _driverActivityService
            .GetActivitiesAsync(from, to, driverCardNumber, driverId, operatingCompanyId, driverIds);

        return Ok(result);
    }

    private static DateTime ToUtc(DateOnly date)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Warsaw");
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified),
            timeZone);
    }
}
