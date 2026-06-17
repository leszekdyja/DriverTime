using DriverTime.Application.Downloads.DTOs;

namespace DriverTime.Application.Downloads;

public interface IDownloadScheduleService
{
    Task<IReadOnlyList<DriverDownloadDto>> GetDriverDownloadsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDownloadDto>> GetVehicleDownloadsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<DownloadDashboardDto> GetDashboardAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);
}
