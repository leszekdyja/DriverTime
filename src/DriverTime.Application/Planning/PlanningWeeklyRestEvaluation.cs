namespace DriverTime.Application.Planning;

public class PlanningWeeklyRestEvaluation
{
    public Guid DriverId { get; init; }

    public DateTime? PeriodStart { get; init; }

    public DateTime? PeriodEnd { get; init; }

    public int? RestMinutes { get; init; }

    public PlanningWeeklyRestKind RestKind { get; init; } = PlanningWeeklyRestKind.None;

    public bool IsEligible { get; init; } = true;

    public string? Warning { get; init; }

    public Guid? PreviousAssignmentId { get; init; }

    public Guid? NextAssignmentId { get; init; }

    public int? RestMinutesBeforeCandidate { get; init; }

    public int? RestMinutesAfterCandidate { get; init; }
}
