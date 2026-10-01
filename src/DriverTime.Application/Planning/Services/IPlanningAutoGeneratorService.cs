using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface IPlanningAutoGeneratorService
{
    Task<PlanningAutoGenerateResultDto> GenerateAsync(PlanningAutoGenerateRequestDto request, CancellationToken cancellationToken = default);

    Task<PlanningAutoGenerateResultDto> PreviewAsync(PlanningAutoGenerateRequestDto request, CancellationToken cancellationToken = default);

    Task<List<PlanningAssignmentListItemDto>> GetAssignmentsAsync(DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken = default);
}
