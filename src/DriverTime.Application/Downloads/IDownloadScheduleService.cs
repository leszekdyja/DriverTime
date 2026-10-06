using DriverTime.Application.Downloads.DTOs;

namespace DriverTime.Application.Downloads;

public interface IDownloadScheduleService
{
    Task<IReadOnlyList<DriverDownloadDto>> GetDriverDownloadsAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VehicleDownloadDto>> GetVehicleDownloadsAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        CancellationToken cancellationToken = default);

    Task<DownloadDashboardDto> GetDashboardAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        CancellationToken cancellationToken = default);
}
