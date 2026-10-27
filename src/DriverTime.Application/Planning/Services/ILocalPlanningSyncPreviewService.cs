using DriverTime.Application.Planning.DTOs;

namespace DriverTime.Application.Planning.Services;

public interface ILocalPlanningSyncPreviewService
{
    Task<LocalPlanningSyncPreviewDto> PreviewAsync(LocalPlanningSnapshotDto snapshot, CancellationToken cancellationToken);
}
