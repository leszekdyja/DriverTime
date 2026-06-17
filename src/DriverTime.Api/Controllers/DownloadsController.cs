using DriverTime.Application.Downloads;
using DriverTime.Application.Downloads.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/downloads")]
public class DownloadsController : ControllerBase
{
    private readonly IDownloadScheduleService _downloadScheduleService;
    private readonly ICurrentUserService _currentUser;

    public DownloadsController(
        IDownloadScheduleService downloadScheduleService,
        ICurrentUserService currentUser)
    {
        _downloadScheduleService = downloadScheduleService;
        _currentUser = currentUser;
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<IReadOnlyList<DriverDownloadDto>>> GetDrivers(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var drivers = await _downloadScheduleService.GetDriverDownloadsAsync(
            _currentUser.CompanyId,
            cancellationToken);

        return Ok(drivers);
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDownloadDto>>> GetVehicles(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var vehicles = await _downloadScheduleService.GetVehicleDownloadsAsync(
            _currentUser.CompanyId,
            cancellationToken);

        return Ok(vehicles);
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DownloadDashboardDto>> GetDashboard(
        CancellationToken cancellationToken)
    {
        if (!HasCompanyContext())
        {
            return Unauthorized();
        }

        var dashboard = await _downloadScheduleService.GetDashboardAsync(
            _currentUser.CompanyId,
            cancellationToken);

        return Ok(dashboard);
    }

    private bool HasCompanyContext()
    {
        return _currentUser.IsAuthenticated && _currentUser.CompanyId != Guid.Empty;
    }
}
