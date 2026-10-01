namespace DriverTime.Application.Planning;

public class PlanningAssignmentConstraintEvaluation
{
    public bool IsForbidden { get; set; }

    public bool IsPreferred { get; set; }

    public decimal PreferenceScore { get; set; }

    public List<string> Matches { get; set; } = new();
}
