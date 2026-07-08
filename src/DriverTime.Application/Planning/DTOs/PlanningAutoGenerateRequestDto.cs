namespace DriverTime.Application.Planning.DTOs;

public class PlanningAutoGenerateRequestDto
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public List<Guid> DriverIds { get; set; } = new();
}

