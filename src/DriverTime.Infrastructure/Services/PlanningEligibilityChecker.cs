using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public class PlanningEligibilityChecker
{
    private readonly PlanningWeeklyRestValidator _weeklyRestValidator = new();


    public PlanningCandidateEvaluation Evaluate(
        Driver driver,
        PlanningDuty duty,
        DateOnly date,
        PlanningWorkInterval? candidateInterval,
        int candidateWorkMinutes,
        IReadOnlyCollection<PlanningAssignment> existingAssignments,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        PlanningGenerationOptions options,
        DateOnly generationDateFrom,
        DateOnly generationDateTo) =>
        Evaluate(
            driver,
            duty,
            date,
            candidateInterval,
            candidateWorkMinutes,
            existingAssignments,
            availabilities,
            options,
            generationDateFrom,
            generationDateTo,
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()));

    public PlanningCandidateEvaluation Evaluate(
        Driver driver,
        PlanningDuty duty,
        DateOnly date,
        PlanningWorkInterval? candidateInterval,
        int candidateWorkMinutes,
        IReadOnlyCollection<PlanningAssignment> existingAssignments,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        PlanningGenerationOptions options,
        DateOnly generationDateFrom,
        DateOnly generationDateTo,
        PlanningWorkingTimeCalendarService calendarService)
    {
        var evaluation = new PlanningCandidateEvaluation
        {
            DriverId = driver.Id,
            DriverName = FormatDriverName(driver),
            DutyId = duty.Id,
            Date = date,
            WeeklyWorkMinutesLimit = options.MaxWeeklyWorkMinutes,
            TargetMonthlyWorkMinutes = options.TargetMonthlyWorkMinutes,
            MaxConsecutiveWorkDays = options.MaxConsecutiveWorkDays
        };

        var driverAssignments = existingAssignments
            .Where(x => x.DriverId == driver.Id)
            .ToList();

        var constraintEvaluation = PlanningAssignmentConstraintEvaluator.Evaluate(driver.CompanyId, driver, duty, date, options.AssignmentRules);
        evaluation.MatchesPreference = constraintEvaluation.IsPreferred;
        evaluation.PreferenceScore = constraintEvaluation.PreferenceScore;
        evaluation.ConstraintMatches.AddRange(constraintEvaluation.Matches);
        if (constraintEvaluation.IsForbidden)
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.DriverDutyForbidden);
        }

        var workloadBefore = PlanningWorkloadCalculator.WorkloadSummary(
            driver.Id,
            driverAssignments,
            availabilities,
            generationDateFrom,
            generationDateTo,
            options,
            calendarService);

        evaluation.WeeklyWorkMinutesBefore = PlanningWorkloadCalculator.WeeklyWorkMinutes(driver.Id, driverAssignments, date, options);
        evaluation.WeeklyWorkMinutesAfter = evaluation.WeeklyWorkMinutesBefore + candidateWorkMinutes;
        evaluation.RealWorkMinutesBefore = workloadBefore.RealWorkMinutes;
        evaluation.RealWorkMinutesAfter = workloadBefore.RealWorkMinutes + candidateWorkMinutes;
        evaluation.CreditedAbsenceMinutes = workloadBefore.CreditedAbsenceMinutes;
        evaluation.MonthlyNormMinutesBefore = workloadBefore.MonthlyNormMinutes;
        evaluation.MonthlyNormMinutesAfter = workloadBefore.MonthlyNormMinutes + candidateWorkMinutes;
        evaluation.MonthlyWorkMinutesBefore = evaluation.MonthlyNormMinutesBefore;
        evaluation.MonthlyWorkMinutesAfter = evaluation.MonthlyNormMinutesAfter;
        evaluation.MonthlyWorkMinutesDeficit = options.TargetMonthlyWorkMinutes.HasValue
            ? options.TargetMonthlyWorkMinutes.Value - evaluation.MonthlyNormMinutesBefore
            : null;
        evaluation.ConsecutiveWorkDaysBefore = PlanningWorkloadCalculator.ConsecutiveWorkDaysBefore(driver.Id, driverAssignments, date);
        evaluation.ConsecutiveWorkDaysAfter = PlanningWorkloadCalculator.ConsecutiveWorkDaysAfter(driver.Id, driverAssignments, date);

        if (candidateInterval is null)
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.DutyMissingStartOrEndTime);
            return evaluation;
        }

        AddAvailabilityRejections(evaluation, driver.Id, date, availabilities);

        if (driverAssignments.Any(x => x.Date == date && x.Status == PlanningAssignmentStatus.Manual))
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.ManualAssignmentOnDate);
        }

        if (driverAssignments.Any(x => x.Date == date && x.Status != PlanningAssignmentStatus.Manual))
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.AlreadyAssignedOnDate);
        }

        var intervals = driverAssignments
            .Where(PlanningWorkloadCalculator.IsWorkAssignment)
            .Select(x => new ExistingPlanningInterval(x, PlanningWorkloadCalculator.ResolveAssignmentInterval(x)))
            .Where(x => x.Interval is not null)
            .Select(x => new ExistingPlanningInterval(x.Assignment, x.Interval))
            .ToList();

        if (intervals.Any(x => Overlaps(candidateInterval.Value, x.Interval!.Value)))
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.OverlappingAssignment);
        }

        var previous = intervals
            .Where(x => x.Interval!.Value.End <= candidateInterval.Value.Start)
            .OrderByDescending(x => x.Interval!.Value.End)
            .FirstOrDefault();
        if (previous is not null)
        {
            evaluation.PreviousAssignmentEnd = previous.Interval!.Value.End;
            evaluation.RestBeforeMinutes = (int)(candidateInterval.Value.Start - previous.Interval.Value.End).TotalMinutes;
            if (evaluation.RestBeforeMinutes < options.MinDailyRestMinutes)
            {
                evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.InsufficientDailyRestBefore);
            }
        }

        var next = intervals
            .Where(x => x.Interval!.Value.Start >= candidateInterval.Value.End)
            .OrderBy(x => x.Interval!.Value.Start)
            .FirstOrDefault();
        if (next is not null)
        {
            evaluation.NextAssignmentStart = next.Interval!.Value.Start;
            evaluation.RestAfterMinutes = (int)(next.Interval.Value.Start - candidateInterval.Value.End).TotalMinutes;
            if (evaluation.RestAfterMinutes < options.MinDailyRestMinutes)
            {
                evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.InsufficientDailyRestAfter);
            }
        }

        var weeklyRest = _weeklyRestValidator.EvaluateCandidate(driver.Id, candidateInterval.Value, driverAssignments, options);
        evaluation.WeeklyRestMinutesBeforeCandidate = weeklyRest.RestMinutesBeforeCandidate;
        evaluation.WeeklyRestMinutesAfterCandidate = weeklyRest.RestMinutesAfterCandidate;
        evaluation.WeeklyRestKind = weeklyRest.RestKind;
        if (!string.IsNullOrWhiteSpace(weeklyRest.Warning))
        {
            evaluation.WeeklyRestWarnings.Add(weeklyRest.Warning);
        }

        if (!weeklyRest.IsEligible)
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.InsufficientWeeklyRest);
        }

        if (evaluation.ConsecutiveWorkDaysAfter > options.MaxConsecutiveWorkDays)
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.TooManyConsecutiveWorkDays);
        }

        if (options.EnforceWeeklyMaximum && evaluation.WeeklyWorkMinutesAfter > options.MaxWeeklyWorkMinutes)
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.WeeklyWorkMinutesExceeded);
        }

        if (options.TargetMonthlyWorkMinutes.HasValue
            && options.EnforceMonthlyTargetMaximum
            && evaluation.MonthlyNormMinutesAfter > options.TargetMonthlyWorkMinutes.Value)
        {
            evaluation.RejectionReasons.Add(PlanningCandidateRejectionReason.MonthlyWorkMinutesExceeded);
        }

        return evaluation;
    }

    public static bool Overlaps(PlanningWorkInterval candidate, PlanningAssignment existing)
    {
        var interval = PlanningWorkloadCalculator.ResolveAssignmentInterval(existing);
        return interval.HasValue && candidate.Start < interval.Value.End && candidate.End > interval.Value.Start;
    }

    public static bool IsWorkAssignment(PlanningAssignment assignment) =>
        PlanningWorkloadCalculator.IsWorkAssignment(assignment);

    public static string FormatDriverName(Driver driver)
    {
        var value = $"{driver.LastName} {driver.FirstName}".Trim();
        return string.IsNullOrWhiteSpace(value) ? driver.CardNumber : value;
    }

    private static bool Overlaps(PlanningWorkInterval candidate, PlanningWorkInterval existing) =>
        candidate.Start < existing.End && candidate.End > existing.Start;

    private static void AddAvailabilityRejections(
        PlanningCandidateEvaluation evaluation,
        Guid driverId,
        DateOnly date,
        IEnumerable<PlanningDriverAvailability> availabilities)
    {
        foreach (var availability in availabilities.Where(x => x.DriverId == driverId && x.DateFrom <= date && x.DateTo >= date))
        {
            var reason = availability.Type switch
            {
                PlanningDriverAvailabilityType.Vacation => PlanningCandidateRejectionReason.Vacation,
                PlanningDriverAvailabilityType.SickLeave => PlanningCandidateRejectionReason.SickLeave,
                PlanningDriverAvailabilityType.DayOff => PlanningCandidateRejectionReason.DayOff,
                PlanningDriverAvailabilityType.Unavailable => PlanningCandidateRejectionReason.Unavailable,
                _ => PlanningCandidateRejectionReason.Unavailable
            };

            if (!evaluation.RejectionReasons.Contains(reason))
            {
                evaluation.RejectionReasons.Add(reason);
            }
        }
    }

    private sealed record ExistingPlanningInterval(PlanningAssignment Assignment, PlanningWorkInterval? Interval);
}



