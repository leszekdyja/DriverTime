namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverPairDto
{
    public Guid Id { get; set; }
    public Guid FirstDriverId { get; set; }
    public string FirstDriverName { get; set; } = string.Empty;
    public Guid SecondDriverId { get; set; }
    public string SecondDriverName { get; set; } = string.Empty;
    public bool IsNightDutyPair { get; set; }
    public bool PreventSameShift { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class PlanningDriverPairRequestDto
{
    public Guid FirstDriverId { get; set; }
    public Guid SecondDriverId { get; set; }
    public bool IsNightDutyPair { get; set; }
    public bool PreventSameShift { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
