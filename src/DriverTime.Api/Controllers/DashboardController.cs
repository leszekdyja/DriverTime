using DriverTime.Application.Dashboard.DTOs;
using DriverTime.Application.DTOs;
using DriverTime.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(from, to, cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("risk-overview")]
    public async Task<ActionResult<DriverRiskOverviewDto>> GetRiskOverview(
        CancellationToken cancellationToken)
    {
        var overview = await _dashboardService.GetRiskOverviewAsync(cancellationToken);
        return Ok(overview);
    }

    [HttpGet("compliance-runs")]
    public async Task<ActionResult<ComplianceRunDashboardStatsDto>> GetComplianceRunStats(
        CancellationToken cancellationToken)
    {
        var stats = await _dashboardService.GetComplianceRunStatsAsync(cancellationToken);
        return Ok(stats);
    }
}
