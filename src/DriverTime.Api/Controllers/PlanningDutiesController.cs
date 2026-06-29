using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/planning/duties")]
public class PlanningDutiesController : ControllerBase
{
    private readonly IPlanningDutyService _planningDutyService;
    private readonly IPlanningDutyPdfImportService _pdfImportService;
    private readonly ICurrentUserService _currentUser;

    public PlanningDutiesController(
        IPlanningDutyService planningDutyService,
        IPlanningDutyPdfImportService pdfImportService,
        ICurrentUserService currentUser)
    {
        _planningDutyService = planningDutyService;
        _pdfImportService = pdfImportService;
        _currentUser = currentUser;
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


    [HttpPost("import/pdf/preview")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<PlanningDutyPdfImportPreviewDto>> PreviewPdfImport(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { errors = new[] { "Wybierz plik PDF do importu." } });
        }

        if (!string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "Do podglądu importu można przesłać tylko plik PDF." } });
        }

        await using var stream = file.OpenReadStream();
        var preview = await _pdfImportService.PreviewAsync(
            file.FileName,
            file.Length,
            stream,
            cancellationToken);

        return Ok(preview);
    }

    [HttpPost("import/pdf/confirm")]
    public async Task<ActionResult<PlanningDutyPdfImportConfirmResultDto>> ConfirmPdfImport(
        [FromBody] PlanningDutyPdfImportConfirmRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _planningDutyService.ConfirmPdfImportAsync(request, cancellationToken);

            return Ok(result);
        }
        catch (PlanningDutyValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
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


