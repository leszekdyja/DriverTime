using DriverTime.Application.Companies.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/operating-companies")]
[Authorize]
public class OperatingCompaniesController : ControllerBase
{
    private readonly IOperatingCompanyService _service;

    public OperatingCompaniesController(IOperatingCompanyService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<OperatingCompanyDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<OperatingCompanyDto>> Create(SaveOperatingCompanyDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return Created($"/api/operating-companies/{result.Id}", result);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OperatingCompanyDto>> Update(Guid id, SaveOperatingCompanyDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
