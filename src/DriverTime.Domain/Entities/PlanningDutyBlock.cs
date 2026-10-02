using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class PlanningDutyBlock : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public Guid FirstDutyId { get; set; }
    public PlanningDuty FirstDuty { get; set; } = null!;
    public Guid SecondDutyId { get; set; }
    public PlanningDuty SecondDuty { get; set; } = null!;
    public string? RequiredVehicleType { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
