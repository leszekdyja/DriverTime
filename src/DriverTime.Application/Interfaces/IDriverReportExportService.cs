using DriverTime.Application.Reports.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverReportExportService
{
    Task<DriverReportDto?> GetReportAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<ReportExportDto?> ExportPdfAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task<ReportExportDto?> ExportExcelAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
