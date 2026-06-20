using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/compliance")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceEngineService _complianceEngine;
    private readonly IComplianceEvaluationService _complianceEvaluationService;
    private readonly IComplianceRunHistoryService _complianceRunHistoryService;
    private readonly ICurrentUserService _currentUser;

    public ComplianceController(
        IComplianceEngineService complianceEngine,
        IComplianceEvaluationService complianceEvaluationService,
        IComplianceRunHistoryService complianceRunHistoryService,
        ICurrentUserService currentUser)
    {
        _complianceEngine = complianceEngine;
        _complianceEvaluationService = complianceEvaluationService;
        _complianceRunHistoryService = complianceRunHistoryService;
        _currentUser = currentUser;
    }

    [HttpGet("drivers/{driverId:guid}/preview")]
    public async Task<ActionResult<CompliancePreviewResponseDto>> PreviewForDriver(
        Guid driverId,
        CancellationToken cancellationToken,
        [FromQuery] bool includeTimeline = false,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized(new
            {
                message = "Wymagane jest zalogowanie użytkownika."
            });
        }

        var preview = await _complianceEngine.PreviewForDriverAsync(
            _currentUser.CompanyId,
            driverId,
            includeTimeline,
            fromUtc,
            toUtc,
            cancellationToken);

        if (preview is null)
        {
            return NotFound(new
            {
                message = "Nie znaleziono kierowcy w bieżącej firmie."
            });
        }

        return Ok(preview);
    }

    [HttpPost("drivers/{driverId:guid}/evaluate")]
    public async Task<ActionResult<ComplianceEvaluationResponseDto>> EvaluateForDriver(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized(new
            {
                message = "Wymagane jest zalogowanie użytkownika."
            });
        }

        var preview = await _complianceEngine.PreviewForDriverAsync(
            _currentUser.CompanyId,
            driverId,
            cancellationToken: cancellationToken);

        if (preview is null)
        {
            return NotFound(new
            {
                message = "Nie znaleziono kierowcy w bieżącej firmie."
            });
        }

        var savedViolationsCount = await _complianceEvaluationService.EvaluateForDriverAsync(
            _currentUser.CompanyId,
            driverId,
            cancellationToken);

        return Ok(new ComplianceEvaluationResponseDto
        {
            DriverId = driverId,
            SavedViolationsCount = savedViolationsCount
        });
    }

    [HttpPost("drivers/{driverId:guid}/runs")]
    public async Task<ActionResult<ComplianceRunDto>> CreateRunForDriver(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized(new
            {
                message = "Wymagane jest zalogowanie użytkownika."
            });
        }

        var preview = await _complianceEngine.PreviewForDriverAsync(
            _currentUser.CompanyId,
            driverId,
            cancellationToken: cancellationToken);

        if (preview is null)
        {
            return NotFound(new
            {
                message = "Nie znaleziono kierowcy w bieżącej firmie."
            });
        }

        var run = await _complianceRunHistoryService.SaveRunAsync(
            _currentUser.CompanyId,
            driverId,
            preview,
            "manual",
            cancellationToken);

        return Ok(run);
    }

    [HttpGet("drivers/{driverId:guid}/runs")]
    public async Task<ActionResult<IReadOnlyList<ComplianceRunDto>>> GetDriverRuns(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized(new
            {
                message = "Wymagane jest zalogowanie użytkownika."
            });
        }

        var runs = await _complianceRunHistoryService.GetDriverRunsAsync(
            _currentUser.CompanyId,
            driverId,
            cancellationToken);

        return Ok(runs);
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<ComplianceRunDto>> GetRun(
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized(new
            {
                message = "Wymagane jest zalogowanie użytkownika."
            });
        }

        var run = await _complianceRunHistoryService.GetRunAsync(
            _currentUser.CompanyId,
            runId,
            cancellationToken);

        return run is null ? NotFound() : Ok(run);
    }
}
