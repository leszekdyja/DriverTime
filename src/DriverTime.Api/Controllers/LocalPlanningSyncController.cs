using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/planning/local-sync")]
public sealed class LocalPlanningSyncController(ILocalPlanningSyncPreviewService service) : ControllerBase
{
    [HttpPost("preview")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<LocalPlanningSyncPreviewDto>> Preview(
        [FromBody] LocalPlanningSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        try { return Ok(await service.PreviewAsync(snapshot, cancellationToken)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
