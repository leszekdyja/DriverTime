using DriverTime.Application.Dashboard.DTOs;
using DriverTime.Application.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<DriverRiskOverviewDto> GetRiskOverviewAsync(
        CancellationToken cancellationToken = default);
}
