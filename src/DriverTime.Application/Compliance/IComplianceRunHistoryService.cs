using DriverTime.Application.Compliance.DTOs;

namespace DriverTime.Application.Compliance;

public interface IComplianceRunHistoryService
{
    Task<ComplianceRunDto> SaveRunAsync(
        Guid companyId,
        Guid driverId,
        CompliancePreviewResponseDto preview,
        string trigger,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ComplianceRunDto>> GetDriverRunsAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken);

    Task<ComplianceRunDto?> GetRunAsync(
        Guid companyId,
        Guid runId,
        CancellationToken cancellationToken);
}
