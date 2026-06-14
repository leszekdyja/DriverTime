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
    public async Task<IActionResult> Upload(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (!Path.GetExtension(file.FileName)
            .Equals(".ddd", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only .ddd files are supported.");
        }

        await using var stream = file.OpenReadStream();

        var importId = await _dddFileService.UploadAndParseAsync(
            stream,
            file.FileName,
            cancellationToken);

        return Ok(new
        {
            id = importId
        });
    }
}