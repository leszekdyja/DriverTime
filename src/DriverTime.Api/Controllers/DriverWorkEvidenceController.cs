using DriverTime.Application.Drivers;
using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api")]
public class DriverWorkEvidenceController : ControllerBase
{
    private readonly IDriverWorkEvidenceService _workEvidenceService;

    public DriverWorkEvidenceController(IDriverWorkEvidenceService workEvidenceService)
    {
        _workEvidenceService = workEvidenceService;
    }

    [HttpGet("drivers/{driverId:guid}/work-evidence")]
    public async Task<ActionResult<DriverWorkEvidenceMonthDto>> GetMonth(
        Guid driverId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await _workEvidenceService.GetMonthAsync(
                driverId,
                year,
                month,
                cancellationToken);

            return evidence is null ? NotFound() : Ok(evidence);
        }
        catch (DriverWorkEvidenceValidationException exception)
        {
            return BadRequest(new { errors = exception.Errors });
        }
    }

    [HttpPost("drivers/{driverId:guid}/work-evidence/entries")]
    public async Task<ActionResult<DriverWorkEvidenceEntryDto>> CreateEntry(
        Guid driverId,
        [FromBody] DriverWorkEvidenceEntryRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _workEvidenceService.CreateEntryAsync(
                driverId,
                request,
                cancellationToken);

            return entry is null ? NotFound() : Ok(entry);
        }
        catch (DriverWorkEvidenceValidationException exception)
        {
            return BadRequest(new { errors = exception.Errors });
        }
    }

    [HttpPut("work-evidence/entries/{entryId:guid}")]
    public async Task<ActionResult<DriverWorkEvidenceEntryDto>> UpdateEntry(
        Guid entryId,
        [FromBody] DriverWorkEvidenceEntryRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _workEvidenceService.UpdateEntryAsync(
                entryId,
                request,
                cancellationToken);

            return entry is null ? NotFound() : Ok(entry);
        }
        catch (DriverWorkEvidenceValidationException exception)
        {
            return BadRequest(new { errors = exception.Errors });
        }
    }

    [HttpDelete("work-evidence/entries/{entryId:guid}")]
    public async Task<IActionResult> DeleteEntry(
        Guid entryId,
        CancellationToken cancellationToken)
    {
        var deleted = await _workEvidenceService.DeleteEntryAsync(
            entryId,
            cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
