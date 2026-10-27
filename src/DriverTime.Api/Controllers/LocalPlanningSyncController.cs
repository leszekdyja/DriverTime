using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/planning/local-sync")]
public sealed class LocalPlanningSyncController(ILocalPlanningSyncPreviewService service, ILocalPlanningImportService importer) : ControllerBase
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

    [HttpGet("options")]
    public Task<ActionResult<LocalPlanningImportOptionsDto>> Options([FromQuery] Guid? operatingCompanyId, CancellationToken ct)
        => Execute(() => importer.GetOptionsAsync(operatingCompanyId, ct));

    [HttpPost("prepare")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public Task<ActionResult<LocalPlanningImportPlanDto>> Prepare([FromBody] LocalPlanningImportRequestDto request, CancellationToken ct)
        => Execute(() => importer.PrepareAsync(request, ct));

    [HttpPost("confirm")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public Task<ActionResult<LocalPlanningImportResultDto>> Confirm([FromBody] LocalPlanningImportRequestDto request, CancellationToken ct)
        => Execute(() => importer.ApplyAsync(request, ct));

    [HttpGet("batches")]
    public Task<ActionResult<List<LocalPlanningBatchListItemDto>>> Batches(CancellationToken ct)
        => Execute(() => importer.GetBatchesAsync(ct));

    [HttpPost("batches/{id:guid}/revert")]
    public Task<ActionResult<object>> Revert(Guid id, CancellationToken ct)
        => Execute<object>(async () => { await importer.RevertAsync(id, ct); return new { reverted = true }; });

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) { return Conflict(new { message = "Grafik zmienił się podczas zapisu. Sprawdź podgląd ponownie." }); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001") { return Conflict(new { message = "Równoległa zmiana grafiku. Sprawdź podgląd ponownie." }); }
    }
}
