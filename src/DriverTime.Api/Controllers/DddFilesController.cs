using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/ddd-files")]
public class DddFilesController : ControllerBase
{
    private readonly IDddParserGateway _dddParserGateway;

    public DddFilesController(IDddParserGateway dddParserGateway)
    {
        _dddParserGateway = dddParserGateway;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (!Path.GetExtension(file.FileName).Equals(".ddd", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .ddd files are supported.");

        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.ddd"
        );

        try
        {
            await using (var stream = System.IO.File.Create(tempFilePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var result = await _dddParserGateway.ParseAsync(tempFilePath, cancellationToken);

            return Ok(result);
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
                System.IO.File.Delete(tempFilePath);
        }
    }
}