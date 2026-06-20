using DriverTime.Application.Dashboard.DTOs;
using DriverTime.Application.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<DriverRiskOverviewDto> GetRiskOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<ComplianceRunDashboardStatsDto> GetComplianceRunStatsAsync(
        CancellationToken cancellationToken = default);
}
