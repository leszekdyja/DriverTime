using DriverTime.Application.Drivers.DTOs;

namespace DriverTime.Application.Interfaces;

public interface IDriverWorkEvidenceService
{
    Task<DriverWorkEvidenceMonthDto?> GetMonthAsync(
        Guid driverId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<DriverWorkEvidenceEntryDto?> CreateEntryAsync(
        Guid driverId,
        DriverWorkEvidenceEntryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DriverWorkEvidenceEntryDto?> UpdateEntryAsync(
        Guid entryId,
        DriverWorkEvidenceEntryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default);
}
