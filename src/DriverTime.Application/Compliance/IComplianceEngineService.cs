using DriverTime.Application.Compliance.DTOs;

namespace DriverTime.Application.Compliance;

public interface IComplianceEngineService
{
    Task<CompliancePreviewResponseDto?> PreviewForDriverAsync(
        Guid companyId,
        Guid driverId,
        bool includeTimeline = false,
        DateTime? rangeStartUtc = null,
        DateTime? rangeEndUtc = null,
        CancellationToken cancellationToken = default);
}
