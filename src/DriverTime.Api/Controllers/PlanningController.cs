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
    private readonly IPlanningDriverDutyRuleService _driverDutyRuleService;
    private readonly IPlanningManualAssignmentService _manualAssignmentService;

    public PlanningController(
        IPlanningAutoGeneratorService autoGeneratorService,
        IPlanningDriverAvailabilityService driverAvailabilityService,
        IPlanningDriverDutyRuleService driverDutyRuleService,
        IPlanningManualAssignmentService manualAssignmentService)
    {
        _autoGeneratorService = autoGeneratorService;
        _driverAvailabilityService = driverAvailabilityService;
        _driverDutyRuleService = driverDutyRuleService;
        _manualAssignmentService = manualAssignmentService;
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


    [HttpGet("assignment-rules")]
    public async Task<ActionResult<List<PlanningDriverDutyRuleDto>>> GetAssignmentRules(
        [FromQuery] Guid? driverId,
        [FromQuery] Guid? dutyId,
        [FromQuery] string? type,
        [FromQuery] DateOnly? activeOn,
        CancellationToken cancellationToken)
    {
        try
        {
            var rules = await _driverDutyRuleService.GetAsync(new PlanningDriverDutyRuleFilterDto
            {
                DriverId = driverId,
                DutyId = dutyId,
                Type = type,
                ActiveOn = activeOn
            }, cancellationToken);
            return Ok(rules);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPost("assignment-rules")]
    public async Task<ActionResult<PlanningDriverDutyRuleDto>> CreateAssignmentRule(
        [FromBody] PlanningDriverDutyRuleRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await _driverDutyRuleService.CreateAsync(request, cancellationToken);
            return Ok(rule);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("assignment-rules/{id:guid}")]
    public async Task<ActionResult<PlanningDriverDutyRuleDto>> UpdateAssignmentRule(
        Guid id,
        [FromBody] PlanningDriverDutyRuleRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await _driverDutyRuleService.UpdateAsync(id, request, cancellationToken);
            return rule is null ? NotFound() : Ok(rule);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpDelete("assignment-rules/{id:guid}")]
    public async Task<IActionResult> DeleteAssignmentRule(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _driverDutyRuleService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("assignments/manual")]
    public async Task<ActionResult<PlanningAssignmentDto>> CreateManualAssignment(
        [FromBody] PlanningManualAssignmentRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await _manualAssignmentService.CreateManualAsync(request, cancellationToken);
            return Ok(assignment);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("assignments/{id:guid}")]
    public async Task<ActionResult<PlanningAssignmentDto>> UpdateAssignment(
        Guid id,
        [FromBody] PlanningManualAssignmentRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await _manualAssignmentService.UpdateAsync(id, request, cancellationToken);
            return assignment is null ? NotFound() : Ok(assignment);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpDelete("assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _manualAssignmentService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
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





