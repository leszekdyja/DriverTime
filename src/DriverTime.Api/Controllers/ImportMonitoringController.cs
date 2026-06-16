using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/import-monitoring")]
public class ImportMonitoringController : ControllerBase
{
    private readonly IDddImportMonitoringService _importMonitoringService;
    private readonly IDddFileService _dddFileService;
    private readonly ImportRetryOptions _retryOptions;

    public ImportMonitoringController(
        IDddImportMonitoringService importMonitoringService,
        IDddFileService dddFileService,
        IOptions<ImportRetryOptions> retryOptions)
    {
        _importMonitoringService = importMonitoringService;
        _dddFileService = dddFileService;
        _retryOptions = retryOptions.Value;
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

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await _importMonitoringService.GetByIdAsync(
            id,
            cancellationToken);

        if (entry is null)
        {
            return NotFound();
        }

        if (entry.RetryCount >= _retryOptions.MaxRetryCount)
        {
            return BadRequest(new
            {
                message = "Osiagnieto maksymalna liczbe prob ponowienia importu."
            });
        }

        var retried = await _dddFileService.RetryImportAsync(
            id,
            cancellationToken);

        return Ok(new
        {
            retried
        });
    }

    [HttpPost("retry-failed")]
    public async Task<IActionResult> RetryFailed(
        CancellationToken cancellationToken)
    {
        var candidates = await _importMonitoringService.GetFailedRetryCandidatesAsync(
            _retryOptions.MaxRetryCount,
            take: 20,
            cancellationToken);
        var retried = 0;

        foreach (var candidate in candidates)
        {
            if (await _dddFileService.RetryImportAsync(candidate.Id, cancellationToken))
            {
                retried++;
            }
        }

        return Ok(new
        {
            retried,
            total = candidates.Count
        });
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
