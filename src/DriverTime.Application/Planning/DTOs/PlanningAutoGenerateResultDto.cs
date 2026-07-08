namespace DriverTime.Application.Planning.DTOs;

public class PlanningAutoGenerateResultDto
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public int GeneratedCount { get; set; }

    public int ConflictCount { get; set; }

    public int ManualAssignmentsPreserved { get; set; }

    public List<string> Messages { get; set; } = new();
}
