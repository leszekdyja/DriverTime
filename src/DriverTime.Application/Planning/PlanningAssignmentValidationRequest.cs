using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public class PlanningAssignmentValidationRequest
{
    public Guid CompanyId { get; set; }

    public Guid? AssignmentId { get; set; }

    public Driver Driver { get; set; } = null!;

    public PlanningDuty PlanningDuty { get; set; } = null!;

    public DateOnly Date { get; set; }

    public IReadOnlyCollection<PlanningAssignment> ExistingAssignments { get; set; } = Array.Empty<PlanningAssignment>();

    public IReadOnlyCollection<PlanningDriverAvailability> Availabilities { get; set; } = Array.Empty<PlanningDriverAvailability>();

    public IReadOnlyCollection<PlanningAssignmentRule> AssignmentRules { get; set; } = Array.Empty<PlanningAssignmentRule>();

    public int MinDailyRestMinutes { get; set; } = PlanningGenerationOptions.DefaultMinDailyRestMinutes;
}
