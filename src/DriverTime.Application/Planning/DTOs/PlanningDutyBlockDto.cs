namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyBlockDto
{
    public Guid Id { get; set; }
    public Guid FirstDutyId { get; set; }
    public string FirstDutyNumber { get; set; } = string.Empty;
    public Guid SecondDutyId { get; set; }
    public string SecondDutyNumber { get; set; } = string.Empty;
    public string? RequiredVehicleType { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class PlanningDutyBlockRequestDto
{
    public Guid FirstDutyId { get; set; }
    public Guid SecondDutyId { get; set; }
    public string? RequiredVehicleType { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
