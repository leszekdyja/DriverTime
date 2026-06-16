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
    private readonly ICurrentUserService _currentUser;

    public ComplianceController(
        IComplianceEngineService complianceEngine,
        IComplianceEvaluationService complianceEvaluationService,
        ICurrentUserService currentUser)
    {
        _complianceEngine = complianceEngine;
        _complianceEvaluationService = complianceEvaluationService;
        _currentUser = currentUser;
    }

    [HttpGet("drivers/{driverId:guid}/preview")]
    public async Task<ActionResult<CompliancePreviewResponseDto>> PreviewForDriver(
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
            cancellationToken);

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
}
