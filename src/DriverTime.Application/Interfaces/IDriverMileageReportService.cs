using DriverTime.Application.Reports.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverMileageReportService
{
    Task<DriverMileageReportDto?> GetReportAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
