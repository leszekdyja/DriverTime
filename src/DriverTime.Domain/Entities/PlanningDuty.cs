using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDuty : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string DutyNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateOnly? ValidFrom { get; set; }

    public string? VehicleRequirement { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public int? TotalDurationMinutes { get; set; }

    public int? WorkMinutes { get; set; }

    public int? BreakMinutes { get; set; }

    public int? DrivingMinutes { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Notes { get; set; }

    public string? SourceFileName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<PlanningDutyLine> Lines { get; set; } = new List<PlanningDutyLine>();

    public ICollection<PlanningDutyStop> Stops { get; set; } = new List<PlanningDutyStop>();

    public ICollection<PlanningAssignment> PlanningAssignments { get; set; } = new List<PlanningAssignment>();
}

