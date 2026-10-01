namespace DriverTime.Application.Planning;

public class PlanningCandidateEvaluation
{
    public Guid DriverId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public Guid DutyId { get; set; }

    public DateOnly Date { get; set; }

    public bool IsEligible => RejectionReasons.Count == 0;

    public decimal Score { get; set; }

    public PlanningCandidateScoreBreakdown ScoreBreakdown { get; set; } = new();

    public List<PlanningCandidateRejectionReason> RejectionReasons { get; set; } = new();

    public DateTime? PreviousAssignmentEnd { get; set; }

    public DateTime? NextAssignmentStart { get; set; }

    public int? RestBeforeMinutes { get; set; }

    public int? RestAfterMinutes { get; set; }

    public int WeeklyWorkMinutesBefore { get; set; }

    public int WeeklyWorkMinutesAfter { get; set; }

    public int WeeklyWorkMinutesLimit { get; set; }

    public int MonthlyWorkMinutesBefore { get; set; }

    public int MonthlyWorkMinutesAfter { get; set; }

    public int RealWorkMinutesBefore { get; set; }

    public int RealWorkMinutesAfter { get; set; }

    public int CreditedAbsenceMinutes { get; set; }

    public int MonthlyNormMinutesBefore { get; set; }

    public int MonthlyNormMinutesAfter { get; set; }

    public int? TargetMonthlyWorkMinutes { get; set; }

    public int? MonthlyWorkMinutesDeficit { get; set; }

    public int ConsecutiveWorkDaysBefore { get; set; }

    public int ConsecutiveWorkDaysAfter { get; set; }

    public int MaxConsecutiveWorkDays { get; set; }

    public int? WeeklyRestMinutesBeforeCandidate { get; set; }

    public int? WeeklyRestMinutesAfterCandidate { get; set; }

    public PlanningWeeklyRestKind WeeklyRestKind { get; set; } = PlanningWeeklyRestKind.None;

    public List<string> WeeklyRestWarnings { get; set; } = new();

    public bool MatchesPreference { get; set; }

    public decimal PreferenceScore { get; set; }

    public List<string> ConstraintMatches { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}
