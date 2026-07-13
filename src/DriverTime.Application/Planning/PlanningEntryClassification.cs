namespace DriverTime.Application.Planning;

public class PlanningEntryClassification
{
    public PlanningEntryKind Kind { get; init; }

    public string Code { get; init; } = string.Empty;

    public bool IsRealWork { get; init; }

    public bool IsTimedWork { get; init; }

    public bool IsRestDay { get; init; }

    public bool CountsTowardWorkMinutes { get; init; }

    public bool CountsTowardMonthlyNorm { get; init; }

    public bool CountsAsConsecutiveWorkDay { get; init; }

    public bool RequiresDuty { get; init; }

    public bool IsGeneratedTechnicalEntry { get; init; }

    public bool IsCreditedAbsence { get; init; }
}
