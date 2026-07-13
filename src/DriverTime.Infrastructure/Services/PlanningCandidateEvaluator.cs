using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public class PlanningCandidateEvaluator
{
    private const decimal MonthlyDeficitWeight = -10m;
    private const decimal CalendarTargetDeficitWeight = -12m;
    private const decimal MonthlyWorkMinutesWeight = 3m;
    private const decimal WeeklyWorkMinutesWeight = 2m;
    private const decimal AssignmentCountWeight = 120m;
    private const decimal ConsecutiveDaysWeight = 60m;
    private const decimal ReducedWeeklyRestPenaltyWeight = 600m;
    private const decimal InsufficientWeeklyRestPenaltyWeight = 5000m;

    private readonly PlanningEligibilityChecker _eligibilityChecker;

    public PlanningCandidateEvaluator(PlanningEligibilityChecker eligibilityChecker)
    {
        _eligibilityChecker = eligibilityChecker;
    }


    public List<PlanningCandidateEvaluation> EvaluateCandidates(
        IReadOnlyList<Driver> drivers,
        PlanningDuty duty,
        DateOnly date,
        IReadOnlyCollection<PlanningAssignment> existingAssignments,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        DateOnly generationDateFrom,
        DateOnly generationDateTo,
        PlanningGenerationOptions options) =>
        EvaluateCandidates(
            drivers,
            duty,
            date,
            existingAssignments,
            availabilities,
            generationDateFrom,
            generationDateTo,
            options,
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));

    public List<PlanningCandidateEvaluation> EvaluateCandidates(
        IReadOnlyList<Driver> drivers,
        PlanningDuty duty,
        DateOnly date,
        IReadOnlyCollection<PlanningAssignment> existingAssignments,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        DateOnly generationDateFrom,
        DateOnly generationDateTo,
        PlanningGenerationOptions options,
        PlanningWorkingTimeCalendarService calendarService)
    {
        PlanningWorkInterval? interval = PlanningWorkInterval.TryCreate(date, duty, out var createdInterval)
            ? createdInterval
            : null;
        var candidateWorkMinutes = interval.HasValue
            ? PlanningWorkloadCalculator.ResolveDutyWorkMinutes(duty, date, interval.Value).WorkMinutes
            : 0;

        var evaluations = new List<PlanningCandidateEvaluation>();
        foreach (var driver in drivers)
        {
            var evaluation = _eligibilityChecker.Evaluate(
                driver,
                duty,
                date,
                interval,
                candidateWorkMinutes,
                existingAssignments,
                availabilities,
                options,
                generationDateFrom,
                generationDateTo,
                calendarService);

            var assignmentCount = PlanningWorkloadCalculator.WorkAssignmentCount(
                driver.Id,
                existingAssignments,
                generationDateFrom,
                generationDateTo,
                options);
            evaluation.ScoreBreakdown = BuildScoreBreakdown(evaluation, assignmentCount);
            evaluation.Score = evaluation.ScoreBreakdown.TotalScore;

            evaluations.Add(evaluation);
        }

        return evaluations;
    }

    public PlanningCandidateEvaluation? ChooseBestCandidate(IReadOnlyCollection<PlanningCandidateEvaluation> evaluations) =>
        evaluations
            .Where(x => x.IsEligible)
            .OrderBy(x => x.ScoreBreakdown.CalendarTargetDeficitScore)
            .ThenBy(x => x.ScoreBreakdown.MonthlyDeficitScore)
            .ThenBy(x => x.ScoreBreakdown.WeeklyRestScore)
            .ThenBy(x => x.ScoreBreakdown.ReducedWeeklyRestPenalty)
            .ThenBy(x => x.ScoreBreakdown.PreferenceScore)
            .ThenBy(x => x.ScoreBreakdown.WorkMinutesScore)
            .ThenBy(x => x.ScoreBreakdown.WeeklyLoadScore)
            .ThenBy(x => x.ScoreBreakdown.AssignmentCountScore)
            .ThenBy(x => x.ScoreBreakdown.ConsecutiveDaysScore)
            .ThenBy(x => x.DriverId)
            .FirstOrDefault();

    private static PlanningCandidateScoreBreakdown BuildScoreBreakdown(
        PlanningCandidateEvaluation evaluation,
        int assignmentCount)
    {
        var monthlyDeficit = evaluation.MonthlyWorkMinutesDeficit.HasValue
            ? Math.Max(0, evaluation.MonthlyWorkMinutesDeficit.Value)
            : 0;
        var weeklyRestScore = evaluation.WeeklyRestKind switch
        {
            PlanningWeeklyRestKind.Regular => 0m,
            PlanningWeeklyRestKind.PreferredReduced => 200m,
            PlanningWeeklyRestKind.Reduced => 400m,
            PlanningWeeklyRestKind.Insufficient => InsufficientWeeklyRestPenaltyWeight,
            _ => 100m
        };
        var reducedPenalty = evaluation.WeeklyRestKind is PlanningWeeklyRestKind.Reduced or PlanningWeeklyRestKind.PreferredReduced
            ? ReducedWeeklyRestPenaltyWeight
            : 0m;

        var breakdown = new PlanningCandidateScoreBreakdown
        {
            AssignmentCountScore = assignmentCount * AssignmentCountWeight,
            WorkMinutesScore = evaluation.MonthlyNormMinutesBefore * MonthlyWorkMinutesWeight,
            MonthlyDeficitScore = -monthlyDeficit * Math.Abs(MonthlyDeficitWeight),
            CalendarTargetDeficitScore = -monthlyDeficit * Math.Abs(CalendarTargetDeficitWeight),
            WeeklyLoadScore = evaluation.WeeklyWorkMinutesBefore * WeeklyWorkMinutesWeight,
            ConsecutiveDaysScore = evaluation.ConsecutiveWorkDaysAfter * ConsecutiveDaysWeight,
            WeeklyRestScore = weeklyRestScore,
            ReducedWeeklyRestPenalty = reducedPenalty,
            PreferenceScore = evaluation.PreferenceScore
        };
        breakdown.TotalScore = breakdown.AssignmentCountScore
            + breakdown.WorkMinutesScore
            + breakdown.MonthlyDeficitScore
            + breakdown.CalendarTargetDeficitScore
            + breakdown.WeeklyLoadScore
            + breakdown.ConsecutiveDaysScore
            + breakdown.WeeklyRestScore
            + breakdown.ReducedWeeklyRestPenalty
            + breakdown.PreferenceScore;

        return breakdown;
    }
}
