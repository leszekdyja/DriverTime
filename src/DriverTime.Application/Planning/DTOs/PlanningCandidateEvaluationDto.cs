using DriverTime.Application.Planning;

namespace DriverTime.Application.Planning.DTOs;

public class PlanningCandidateEvaluationDto
{
    public Guid DriverId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public Guid DutyId { get; set; }

    public DateOnly Date { get; set; }

    public bool IsEligible { get; set; }

    public decimal Score { get; set; }

    public PlanningCandidateScoreBreakdownDto ScoreBreakdown { get; set; } = new();

    public List<string> RejectionReasons { get; set; } = new();

    public List<string> RejectionReasonDescriptions { get; set; } = new();

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

    public string WeeklyRestKind { get; set; } = PlanningWeeklyRestKind.None.ToString();

    public List<string> WeeklyRestWarnings { get; set; } = new();

    public bool MatchesPreference { get; set; }

    public decimal PreferenceScore { get; set; }

    public List<string> ConstraintMatches { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public static PlanningCandidateEvaluationDto FromEvaluation(PlanningCandidateEvaluation evaluation) => new()
    {
        DriverId = evaluation.DriverId,
        DriverName = evaluation.DriverName,
        DutyId = evaluation.DutyId,
        Date = evaluation.Date,
        IsEligible = evaluation.IsEligible,
        Score = evaluation.Score,
        ScoreBreakdown = new PlanningCandidateScoreBreakdownDto
        {
            AssignmentCountScore = evaluation.ScoreBreakdown.AssignmentCountScore,
            WorkMinutesScore = evaluation.ScoreBreakdown.WorkMinutesScore,
            MonthlyDeficitScore = evaluation.ScoreBreakdown.MonthlyDeficitScore,
            CalendarTargetDeficitScore = evaluation.ScoreBreakdown.CalendarTargetDeficitScore,
            WeeklyLoadScore = evaluation.ScoreBreakdown.WeeklyLoadScore,
            ConsecutiveDaysScore = evaluation.ScoreBreakdown.ConsecutiveDaysScore,
            WeeklyRestScore = evaluation.ScoreBreakdown.WeeklyRestScore,
            ReducedWeeklyRestPenalty = evaluation.ScoreBreakdown.ReducedWeeklyRestPenalty,
            PreferenceScore = evaluation.ScoreBreakdown.PreferenceScore,
            TotalScore = evaluation.ScoreBreakdown.TotalScore
        },
        RejectionReasons = evaluation.RejectionReasons.Select(x => x.ToString()).ToList(),
        RejectionReasonDescriptions = evaluation.RejectionReasons.Select(PlanningCandidateRejectionReasonDescriptions.ToPolishDescription).ToList(),
        PreviousAssignmentEnd = evaluation.PreviousAssignmentEnd,
        NextAssignmentStart = evaluation.NextAssignmentStart,
        RestBeforeMinutes = evaluation.RestBeforeMinutes,
        RestAfterMinutes = evaluation.RestAfterMinutes,
        WeeklyWorkMinutesBefore = evaluation.WeeklyWorkMinutesBefore,
        WeeklyWorkMinutesAfter = evaluation.WeeklyWorkMinutesAfter,
        WeeklyWorkMinutesLimit = evaluation.WeeklyWorkMinutesLimit,
        MonthlyWorkMinutesBefore = evaluation.MonthlyWorkMinutesBefore,
        MonthlyWorkMinutesAfter = evaluation.MonthlyWorkMinutesAfter,
        RealWorkMinutesBefore = evaluation.RealWorkMinutesBefore,
        RealWorkMinutesAfter = evaluation.RealWorkMinutesAfter,
        CreditedAbsenceMinutes = evaluation.CreditedAbsenceMinutes,
        MonthlyNormMinutesBefore = evaluation.MonthlyNormMinutesBefore,
        MonthlyNormMinutesAfter = evaluation.MonthlyNormMinutesAfter,
        TargetMonthlyWorkMinutes = evaluation.TargetMonthlyWorkMinutes,
        MonthlyWorkMinutesDeficit = evaluation.MonthlyWorkMinutesDeficit,
        ConsecutiveWorkDaysBefore = evaluation.ConsecutiveWorkDaysBefore,
        ConsecutiveWorkDaysAfter = evaluation.ConsecutiveWorkDaysAfter,
        MaxConsecutiveWorkDays = evaluation.MaxConsecutiveWorkDays,
        WeeklyRestMinutesBeforeCandidate = evaluation.WeeklyRestMinutesBeforeCandidate,
        WeeklyRestMinutesAfterCandidate = evaluation.WeeklyRestMinutesAfterCandidate,
        WeeklyRestKind = evaluation.WeeklyRestKind.ToString(),
        WeeklyRestWarnings = evaluation.WeeklyRestWarnings.ToList(),
        MatchesPreference = evaluation.MatchesPreference,
        PreferenceScore = evaluation.PreferenceScore,
        ConstraintMatches = evaluation.ConstraintMatches.ToList(),
        Warnings = evaluation.Warnings.ToList()
    };
}
