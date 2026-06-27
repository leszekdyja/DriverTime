using DriverTime.Application.DDD.Exceptions;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/ddd-files")]
public class DddFilesController : ControllerBase
{
    private readonly IDddFileService _dddFileService;

    public DddFilesController(IDddFileService dddFileService)
    {
        _dddFileService = dddFileService;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (!Path.GetExtension(file.FileName)
            .Equals(".ddd", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .ddd files are supported.");
        }

        await using var stream = file.OpenReadStream();

        try
        {
            var result = await _dddFileService.UploadAndParseAsync(
                stream,
                file.FileName);

            return Ok(result);
        }
        catch (DuplicateDddFileException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _dddFileService.GetAllAsync();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _dddFileService.GetByIdAsync(id);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _dddFileService.DeleteAsync(id, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
