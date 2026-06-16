using DriverTime.Domain.Compliance;

namespace DriverTime.Application.Compliance;

public interface ITimelineBuilderService
{
    Task<IReadOnlyList<TimelineActivity>?> BuildForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default);
}
