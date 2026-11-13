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

    public DriversController(
        IDriverService driverService,
        IDriverViolationService driverViolationService,
        IDriverActivityCalendarService activityCalendarService)
    {
        _driverService = driverService;
        _driverViolationService = driverViolationService;
        _activityCalendarService = activityCalendarService;
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

    [HttpPatch("{id:guid}/operating-company")]
    public async Task<ActionResult<DriverDto>> UpdateOperatingCompany(
        Guid id,
        [FromBody] UpdateDriverOperatingCompanyDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var driver = await _driverService.UpdateOperatingCompanyAsync(id, dto, cancellationToken);
            return driver is null ? NotFound() : Ok(driver);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/mobile-invite")]
    public ActionResult<DriverMobileAppInviteDto> CreateMobileInvite(
        Guid id,
        CancellationToken cancellationToken)
    {
        return StatusCode(StatusCodes.Status410Gone, new { message = "Integracja z aplikacją mobilną została wyłączona." });
    }

    [HttpDelete("{id:guid}")]
    // Deletion archives the profile without deleting historical data.
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _driverService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("deleted")]
    public async Task<ActionResult<List<DeletedDriverDto>>> GetDeleted(CancellationToken cancellationToken) =>
        Ok(await _driverService.GetDeletedAsync(cancellationToken));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        try { return await _driverService.RestoreAsync(id, cancellationToken) ? NoContent() : NotFound(); }
        catch (InvalidOperationException exception) { return StatusCode(StatusCodes.Status410Gone, new { message = exception.Message }); }
    }
}

