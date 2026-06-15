using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IDriverReportExportService _reportExportService;

    public ReportsController(IDriverReportExportService reportExportService)
    {
        _reportExportService = reportExportService;
    }

    [HttpGet("driver/{driverId:guid}")]
    public async Task<IActionResult> GetDriverReport(
        Guid driverId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return BadRequest(new { message = "Data from nie moze byc pozniejsza niz data to." });
        }

        var report = await _reportExportService.GetReportAsync(
            driverId,
            from,
            to,
            cancellationToken);

        return report is null ? NotFound() : Ok(report);
    }

    [HttpGet("driver/{driverId:guid}/export/pdf")]
    public async Task<IActionResult> ExportDriverPdf(
        Guid driverId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return BadRequest(new { message = "Data from nie moze byc pozniejsza niz data to." });
        }

        var export = await _reportExportService.ExportPdfAsync(
            driverId,
            from,
            to,
            cancellationToken);

        return export is null
            ? NotFound()
            : File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("driver/{driverId:guid}/export/excel")]
    public async Task<IActionResult> ExportDriverExcel(
        Guid driverId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        if (from > to)
        {
            return BadRequest(new { message = "Data from nie moze byc pozniejsza niz data to." });
        }

        var export = await _reportExportService.ExportExcelAsync(
            driverId,
            from,
            to,
            cancellationToken);

        return export is null
            ? NotFound()
            : File(export.Content, export.ContentType, export.FileName);
    }
}
