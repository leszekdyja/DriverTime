using DriverTime.Application.Compliance.DTOs;

namespace DriverTime.Application.Compliance;

public interface IComplianceEngineService
{
    Task<CompliancePreviewResponseDto?> PreviewForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default);
}
