using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningDutyService
{
    Task<List<PlanningDutyListDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PlanningDutyDetailsDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlanningDutyDetailsDto> CreateAsync(CreatePlanningDutyRequest request, CancellationToken cancellationToken = default);

    Task<PlanningDutyDetailsDto?> UpdateAsync(Guid id, UpdatePlanningDutyRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PlanningDutyPdfImportConfirmResultDto> ConfirmPdfImportAsync(
        PlanningDutyPdfImportConfirmRequestDto request,
        CancellationToken cancellationToken = default);
}

