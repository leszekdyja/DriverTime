using DriverTime.Application.Violations.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IViolationQueryService
{
    Task<IReadOnlyList<ViolationDto>> GetAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        Guid? driverId,
        DateTime? fromDate,
        DateTime? toDate,
        string? severity,
        string? type,
        CancellationToken cancellationToken = default);

    Task<ViolationDto?> GetByIdAsync(
        Guid companyId,
        Guid? operatingCompanyId,
        Guid id,
        CancellationToken cancellationToken = default);
}
