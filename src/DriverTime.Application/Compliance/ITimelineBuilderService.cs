using DriverTime.Domain.Compliance;

namespace DriverTime.Application.Compliance;

public interface ITimelineBuilderService
{
    Task<IReadOnlyList<TimelineActivity>?> BuildForDriverAsync(
        Guid companyId,
        Guid driverId,
        DateTime? queryStartUtc = null,
        DateTime? queryEndUtc = null,
        CancellationToken cancellationToken = default);
}
