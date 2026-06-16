using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/import-monitoring")]
public class ImportMonitoringController : ControllerBase
{
    private readonly IDddImportMonitoringService _importMonitoringService;

    public ImportMonitoringController(
        IDddImportMonitoringService importMonitoringService)
    {
        _importMonitoringService = importMonitoringService;
    }

    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _importMonitoringService.GetRecentAsync(
            take,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _importMonitoringService.GetByIdAsync(
            id,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            module = "import-monitoring"
        });
    }
}
