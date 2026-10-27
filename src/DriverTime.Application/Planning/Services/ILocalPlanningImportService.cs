using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface ILocalPlanningImportService
{
    Task<LocalPlanningImportOptionsDto> GetOptionsAsync(Guid? operatingCompanyId, CancellationToken cancellationToken);
    Task<LocalPlanningImportPlanDto> PrepareAsync(LocalPlanningImportRequestDto request, CancellationToken cancellationToken);
    Task<LocalPlanningImportResultDto> ApplyAsync(LocalPlanningImportRequestDto request, CancellationToken cancellationToken);
    Task<List<LocalPlanningBatchListItemDto>> GetBatchesAsync(CancellationToken cancellationToken);
    Task RevertAsync(Guid batchId, CancellationToken cancellationToken);
}
