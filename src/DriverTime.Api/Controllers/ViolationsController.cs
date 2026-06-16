using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/violations")]
public class ViolationsController : ControllerBase
{
    private readonly IViolationDetectionService _violationDetectionService;
    private readonly IViolationQueryService _violationQueryService;
    private readonly ICurrentUserService _currentUser;

    public ViolationsController(
        IViolationDetectionService violationDetectionService,
        IViolationQueryService violationQueryService,
        ICurrentUserService currentUser)
    {
        _violationDetectionService = violationDetectionService;
        _violationQueryService = violationQueryService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ViolationDto>>> GetAll(
        [FromQuery] Guid? driverId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? severity,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        var violations = await _violationQueryService.GetAsync(
            _currentUser.CompanyId,
            driverId,
            fromDate,
            toDate,
            severity,
            type,
            cancellationToken);

        return Ok(violations);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ViolationDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        var violation = await _violationQueryService.GetByIdAsync(
            _currentUser.CompanyId,
            id,
            cancellationToken);

        return violation is null ? NotFound() : Ok(violation);
    }

    [HttpPost("detect")]
    public async Task<ActionResult<DetectViolationsResponse>> Detect(
        [FromBody] DetectViolationsRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (request.DriverId == Guid.Empty)
        {
            return BadRequest(new { message = "DriverId jest wymagany." });
        }

        if (request.FromDate > request.ToDate)
        {
            return BadRequest(new { message = "FromDate nie może być późniejsza niż ToDate." });
        }

        var detectedCount = await _violationDetectionService.DetectForDriverAsync(
            _currentUser.CompanyId,
            request.DriverId,
            request.FromDate,
            request.ToDate,
            cancellationToken);

        return Ok(new DetectViolationsResponse
        {
            DetectedCount = detectedCount
        });
    }
}
