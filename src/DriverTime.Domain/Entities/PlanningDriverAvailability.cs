using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDriverAvailability : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public PlanningDriverAvailabilityType Type { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
