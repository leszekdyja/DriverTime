using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController : ControllerBase
{
    private readonly IDriverService _driverService;
    private readonly IDriverViolationService _driverViolationService;
    private readonly IDriverActivityCalendarService _activityCalendarService;
    private readonly IDriverMobileAppInviteService _mobileAppInviteService;
    private readonly IConfiguration _configuration;

    public DriversController(
        IDriverService driverService,
        IDriverViolationService driverViolationService,
        IDriverActivityCalendarService activityCalendarService,
        IDriverMobileAppInviteService mobileAppInviteService,
        IConfiguration configuration)
    {
        _driverService = driverService;
        _driverViolationService = driverViolationService;
        _activityCalendarService = activityCalendarService;
        _mobileAppInviteService = mobileAppInviteService;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<List<DriverDto>>> GetAll()
    {
        var drivers = await _driverService.GetAllAsync();

        return Ok(drivers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DriverDetailsDto>> GetById(Guid id)
    {
        var driver = await _driverService.GetByIdAsync(id);

        if (driver == null)
        {
            return NotFound();
        }

        return Ok(driver);
    }

    [HttpGet("{id:guid}/violations")]
    public async Task<ActionResult<IReadOnlyList<DriverViolationDto>>> GetViolations(
        Guid id,
        CancellationToken cancellationToken)
    {
        var violations = await _driverViolationService
            .GetViolationsForDriverAsync(id, cancellationToken);

        return violations is null ? NotFound() : Ok(violations);
    }

    [HttpGet("{driverId:guid}/activity-calendar")]
    public async Task<ActionResult<DriverActivityCalendarDto>> GetActivityCalendar(
        Guid driverId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return BadRequest(new
            {
                message = "Data from nie moze byc pozniejsza niz data to."
            });
        }

        var calendar = await _activityCalendarService.GetAsync(
            driverId,
            from,
            to,
            cancellationToken);

        return calendar is null ? NotFound() : Ok(calendar);
    }

    [HttpPost]
    public async Task<ActionResult<DriverDto>> Create(
        [FromBody] CreateDriverDto dto)
    {
        var createdDriver = await _driverService.CreateAsync(dto);

        return Ok(createdDriver);
    }


    [HttpPatch("{id:guid}/planning")]
    public async Task<ActionResult<DriverDto>> UpdatePlanning(
        Guid id,
        [FromBody] UpdateDriverPlanningDto dto,
        CancellationToken cancellationToken)
    {
        var driver = await _driverService.UpdatePlanningAsync(id, dto, cancellationToken);

        return driver is null ? NotFound() : Ok(driver);
    }

    [HttpPost("{id:guid}/mobile-invite")]
    public async Task<ActionResult<DriverMobileAppInviteDto>> CreateMobileInvite(
        Guid id,
        CancellationToken cancellationToken)
    {
        var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var mobileSetupBaseUrl = BuildMobileSetupBaseUrl();
        var invite = await _mobileAppInviteService.CreateInviteAsync(
            id,
            apiBaseUrl,
            mobileSetupBaseUrl,
            cancellationToken);

        return invite is null ? NotFound() : Ok(invite);
    }

    private string BuildMobileSetupBaseUrl()
    {
        var configured = _configuration["PublicAppUrl"]
            ?? _configuration["PUBLIC_APP_URL"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return $"{configured.TrimEnd('/')}/mobile/setup";
        }

        var host = Request.Host.Host;
        var port = Request.Host.Port == 8080 ? 3000 : Request.Host.Port;
        var hostString = port.HasValue ? $"{host}:{port.Value}" : host;

        return $"{Request.Scheme}://{hostString}/mobile/setup";
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _driverService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}

