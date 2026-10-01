using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDutyStop : BaseEntity
{
    public Guid PlanningDutyId { get; set; }

    public PlanningDuty PlanningDuty { get; set; } = null!;

    public int Sequence { get; set; }

    public string StopName { get; set; } = string.Empty;

    public decimal? Km { get; set; }

    public string? TripGroup { get; set; }

    public TimeOnly? ArrivalTime { get; set; }

    public TimeOnly? DepartureTime { get; set; }

    public string? LineCode { get; set; }
}
