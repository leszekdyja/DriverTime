using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningDutyPdfImportService
{
    Task<PlanningDutyPdfImportPreviewDto> PreviewAsync(
        string fileName,
        long fileSizeBytes,
        Stream pdfStream,
        CancellationToken cancellationToken = default);
}
