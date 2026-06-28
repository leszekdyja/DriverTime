using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/planning/duties")]
public class PlanningDutiesController : ControllerBase
{
    private readonly IPlanningDutyService _planningDutyService;

    public PlanningDutiesController(IPlanningDutyService planningDutyService)
    {
        _planningDutyService = planningDutyService;
    }

    [HttpGet]
    public async Task<ActionResult<List<PlanningDutyListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var duties = await _planningDutyService.GetAllAsync(cancellationToken);

        return Ok(duties);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlanningDutyDetailsDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var duty = await _planningDutyService.GetByIdAsync(id, cancellationToken);

        return duty is null ? NotFound() : Ok(duty);
    }

    [HttpPost]
    public async Task<ActionResult<PlanningDutyDetailsDto>> Create(
        [FromBody] CreatePlanningDutyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var duty = await _planningDutyService.CreateAsync(request, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = duty.Id }, duty);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlanningDutyDetailsDto>> Update(
        Guid id,
        [FromBody] UpdatePlanningDutyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var duty = await _planningDutyService.UpdateAsync(id, request, cancellationToken);

            return duty is null ? NotFound() : Ok(duty);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _planningDutyService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
