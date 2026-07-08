using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/planning")]
public class PlanningController : ControllerBase
{
    private readonly IPlanningAutoGeneratorService _autoGeneratorService;
    private readonly IPlanningDriverAvailabilityService _driverAvailabilityService;

    public PlanningController(
        IPlanningAutoGeneratorService autoGeneratorService,
        IPlanningDriverAvailabilityService driverAvailabilityService)
    {
        _autoGeneratorService = autoGeneratorService;
        _driverAvailabilityService = driverAvailabilityService;
    }

    [HttpPost("auto-generate")]
    public async Task<ActionResult<PlanningAutoGenerateResultDto>> AutoGenerate(
        [FromBody] PlanningAutoGenerateRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _autoGeneratorService.GenerateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<List<PlanningAssignmentListItemDto>>> GetAssignments(
        [FromQuery] DateOnly dateFrom,
        [FromQuery] DateOnly dateTo,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignments = await _autoGeneratorService.GetAssignmentsAsync(dateFrom, dateTo, cancellationToken);
            return Ok(assignments);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpGet("driver-availability")]
    public async Task<ActionResult<List<PlanningDriverAvailabilityDto>>> GetDriverAvailability(
        [FromQuery] DateOnly dateFrom,
        [FromQuery] DateOnly dateTo,
        CancellationToken cancellationToken)
    {
        try
        {
            var availability = await _driverAvailabilityService.GetAsync(dateFrom, dateTo, cancellationToken);
            return Ok(availability);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPost("driver-availability")]
    public async Task<ActionResult<PlanningDriverAvailabilityDto>> CreateDriverAvailability(
        [FromBody] PlanningDriverAvailabilityCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var availability = await _driverAvailabilityService.CreateAsync(request, cancellationToken);
            return Ok(availability);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpDelete("driver-availability/{id:guid}")]
    public async Task<IActionResult> DeleteDriverAvailability(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _driverAvailabilityService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}


