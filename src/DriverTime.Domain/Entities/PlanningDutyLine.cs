using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDutyLine : BaseEntity
{
    public Guid PlanningDutyId { get; set; }

    public PlanningDuty PlanningDuty { get; set; } = null!;

    public string LineCode { get; set; } = string.Empty;

    public string? Variant { get; set; }

    public decimal? DistanceKm { get; set; }
}
