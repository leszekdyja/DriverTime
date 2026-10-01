using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/planning/schedules")]
public class PlanningSchedulesController : ControllerBase
{
    private readonly IPlanningScheduleService _planningScheduleService;
    private readonly IPlanningScheduleValidationService _validationService;

    public PlanningSchedulesController(
        IPlanningScheduleService planningScheduleService,
        IPlanningScheduleValidationService validationService)
    {
        _planningScheduleService = planningScheduleService;
        _validationService = validationService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PlanningScheduleListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var schedules = await _planningScheduleService.GetSchedulesAsync(cancellationToken);

        return Ok(schedules);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanningScheduleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await _planningScheduleService.GetScheduleAsync(id, cancellationToken);

        return schedule is null ? NotFound() : Ok(schedule);
    }


    [HttpGet("{id:guid}/validation")]
    public async Task<ActionResult<PlanningScheduleValidationDto>> ValidateSchedule(
        Guid id,
        CancellationToken cancellationToken)
    {
        var validation = await _validationService.ValidateScheduleAsync(id, cancellationToken);

        return validation is null ? NotFound() : Ok(validation);
    }
    [HttpPost]
    public async Task<ActionResult<PlanningScheduleDto>> Create(
        [FromBody] PlanningScheduleCreateRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await _planningScheduleService.CreateScheduleAsync(request, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, schedule);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlanningScheduleDto>> Update(
        Guid id,
        [FromBody] PlanningScheduleUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var schedule = await _planningScheduleService.UpdateScheduleAsync(id, request, cancellationToken);

            return schedule is null ? NotFound() : Ok(schedule);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _planningScheduleService.DeleteScheduleAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/assignments")]
    public async Task<ActionResult<PlanningAssignmentDto>> UpsertAssignment(
        Guid id,
        [FromBody] PlanningAssignmentUpsertRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assignment = await _planningScheduleService.UpsertAssignmentAsync(id, request, cancellationToken);

            return assignment is null ? NotFound() : Ok(assignment);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    public async Task<IActionResult> DeleteAssignment(
        Guid id,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var deleted = await _planningScheduleService.DeleteAssignmentAsync(id, assignmentId, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}

