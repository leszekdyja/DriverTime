using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public class PlanningAssignmentSwapPlanner
{
    private const int MaxChainDepth = 4;

    private readonly PlanningEligibilityChecker _eligibilityChecker;
    private readonly PlanningWorkingTimeCalendarService _calendarService;

    public PlanningAssignmentSwapPlanner(
        PlanningEligibilityChecker eligibilityChecker,
        PlanningWorkingTimeCalendarService calendarService)
    {
        _eligibilityChecker = eligibilityChecker;
        _calendarService = calendarService;
    }

    public PlanningSwapRescueResult RescueUnassignedDuties(
        IReadOnlyList<Driver> drivers,
        IReadOnlyCollection<PlanningDuty> duties,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        List<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        IReadOnlyCollection<PlanningUnassignedDutyDto> unassignedDuties,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        Guid companyId,
        DateTime now)
    {
        var result = new PlanningSwapRescueResult();
        var dutiesById = duties.ToDictionary(x => x.Id);
        var driversById = drivers.ToDictionary(x => x.Id);

        foreach (var unassigned in unassignedDuties.ToList())
        {
            if (!dutiesById.TryGetValue(unassigned.DutyId, out var missingDuty))
            {
                continue;
            }

            if (PlanningEntryClassifier.Classify(missingDuty).Kind != PlanningEntryKind.Duty)
            {
                continue;
            }

            var plan = BuildDirectPlan(driversById, schedules, assignmentContext, availabilities, missingDuty, unassigned.Date, dateFrom, dateTo, options, companyId, now)
                ?? BuildChainPlan(driversById, schedules, assignmentContext, availabilities, missingDuty, unassigned.Date, dateFrom, dateTo, options, companyId, now);

            if (plan is null)
            {
                continue;
            }

            ApplyPlan(plan, assignmentContext);
            result.Plans.Add(plan);
            result.ResolvedUnassignedDutyIds.Add((unassigned.DutyId, unassigned.Date));
            result.RescuedDutyCount++;
            if (plan.IsChain)
            {
                result.ChainSwapCount++;
            }
            else
            {
                result.DirectSwapCount++;
            }

            foreach (var removed in plan.RemovedAssignments)
            {
                switch (PlanningEntryClassifier.Classify(removed).Kind)
                {
                    case PlanningEntryKind.WeeklyDayOff:
                        result.RemovedWeeklyDayOffCount++;
                        break;
                    case PlanningEntryKind.DayOff:
                        result.RemovedDayOffCount++;
                        break;
                    case PlanningEntryKind.ReserveFirstShift:
                        result.RemovedReserveFirstShiftCount++;
                        break;
                    case PlanningEntryKind.ReserveSecondShift:
                        result.RemovedReserveSecondShiftCount++;
                        break;
                }
            }
        }

        return result;
    }

    private PlanningSwapPlan? BuildDirectPlan(
        IReadOnlyDictionary<Guid, Driver> driversById,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        PlanningDuty missingDuty,
        DateOnly date,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        Guid companyId,
        DateTime now)
    {
        foreach (var source in RemovableTechnicalAssignments(assignmentContext, date).OrderBy(TechnicalPriority).ThenBy(x => x.DriverId))
        {
            if (!driversById.TryGetValue(source.DriverId, out var driver))
            {
                continue;
            }

            if (!CanAssign(driver, missingDuty, date, assignmentContext, availabilities, new[] { source }, Array.Empty<PlanningAssignment>(), dateFrom, dateTo, options))
            {
                continue;
            }

            var plan = new PlanningSwapPlan { IsChain = false };
            plan.RemovedAssignments.Add(source);
            plan.AddedAssignments.Add(CreateGeneratedAssignment(companyId, schedules, source.DriverId, missingDuty, date, now, $"Ratunek braku: {missingDuty.DutyNumber} zamiast {PlanningEntryClassifier.Classify(source).Code}."));
            plan.Steps.Add(new PlanningSwapStep(source.DriverId, source.PlanningDutyId, missingDuty.Id, date, "direct"));
            return ValidateWholePlan(plan, assignmentContext, availabilities, dateFrom, dateTo, options) ? plan : null;
        }

        return null;
    }

    private PlanningSwapPlan? BuildChainPlan(
        IReadOnlyDictionary<Guid, Driver> driversById,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        PlanningDuty missingDuty,
        DateOnly date,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        Guid companyId,
        DateTime now)
    {
        var candidates = assignmentContext
            .Where(x => x.Date == date && IsMovableGeneratedRealDuty(x) && x.PlanningDuty is not null)
            .OrderBy(x => PlanningWorkloadCalculator.ResolveAssignmentWorkMinutes(x))
            .ThenBy(x => x.DriverId)
            .ToList();

        foreach (var source in candidates)
        {
            if (!driversById.TryGetValue(source.DriverId, out var sourceDriver))
            {
                continue;
            }

            var plan = new PlanningSwapPlan { IsChain = true };
            plan.RemovedAssignments.Add(source);
            if (!CanAssign(sourceDriver, missingDuty, date, assignmentContext, availabilities, plan.RemovedAssignments, plan.AddedAssignments, dateFrom, dateTo, options))
            {
                continue;
            }

            if (!TryMoveDisplacedDuty(source, driversById, schedules, assignmentContext, availabilities, plan, dateFrom, dateTo, options, companyId, now, 0, new HashSet<Guid> { source.DriverId }))
            {
                continue;
            }

            plan.AddedAssignments.Add(CreateGeneratedAssignment(companyId, schedules, source.DriverId, missingDuty, date, now, $"Ratunek łańcuchowy: {missingDuty.DutyNumber} zamiast {source.PlanningDuty!.DutyNumber}."));
            plan.Steps.Add(new PlanningSwapStep(source.DriverId, source.PlanningDutyId, missingDuty.Id, date, "chain-target"));
            if (ValidateWholePlan(plan, assignmentContext, availabilities, dateFrom, dateTo, options))
            {
                return plan;
            }
        }

        return null;
    }

    private bool TryMoveDisplacedDuty(
        PlanningAssignment displacedAssignment,
        IReadOnlyDictionary<Guid, Driver> driversById,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        PlanningSwapPlan plan,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        Guid companyId,
        DateTime now,
        int depth,
        HashSet<Guid> visitedDrivers)
    {
        if (depth >= MaxChainDepth || displacedAssignment.PlanningDuty is null)
        {
            return false;
        }

        var date = displacedAssignment.Date;
        foreach (var receiver in CandidateReceivers(assignmentContext, date, plan).Where(x => !visitedDrivers.Contains(x.DriverId)))
        {
            if (!driversById.TryGetValue(receiver.DriverId, out var receiverDriver))
            {
                continue;
            }

            var receiverDuty = receiver.PlanningDuty;
            var receiverKind = PlanningEntryClassifier.Classify(receiver).Kind;
            var removedCount = plan.RemovedAssignments.Count;
            var addedCount = plan.AddedAssignments.Count;
            var stepsCount = plan.Steps.Count;
            plan.RemovedAssignments.Add(receiver);
            visitedDrivers.Add(receiver.DriverId);

            if (CanAssign(receiverDriver, displacedAssignment.PlanningDuty, date, assignmentContext, availabilities, plan.RemovedAssignments, plan.AddedAssignments, dateFrom, dateTo, options))
            {
                var mustMoveReceiverDuty = receiverDuty is not null && IsMovableGeneratedRealDuty(receiver);
                var receiverMoved = !mustMoveReceiverDuty || TryMoveDisplacedDuty(receiver, driversById, schedules, assignmentContext, availabilities, plan, dateFrom, dateTo, options, companyId, now, depth + 1, visitedDrivers);
                if (receiverMoved)
                {
                    plan.AddedAssignments.Add(CreateGeneratedAssignment(companyId, schedules, receiver.DriverId, displacedAssignment.PlanningDuty, date, now, $"Ratunek łańcuchowy: przejęcie {displacedAssignment.PlanningDuty.DutyNumber}."));
                    plan.Steps.Add(new PlanningSwapStep(receiver.DriverId, receiver.PlanningDutyId, displacedAssignment.PlanningDutyId, date, receiverKind.ToString()));
                    return true;
                }
            }

            visitedDrivers.Remove(receiver.DriverId);
            RollbackPlanTail(plan, removedCount, addedCount, stepsCount);
        }

        return false;
    }

    private IEnumerable<PlanningAssignment> CandidateReceivers(
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        DateOnly date,
        PlanningSwapPlan plan)
    {
        var removedIds = plan.RemovedAssignments.Select(x => x.Id).ToHashSet();
        return assignmentContext
            .Where(x => x.Date == date && !removedIds.Contains(x.Id))
            .Where(x => IsRemovableTechnicalAssignment(x) || IsMovableGeneratedRealDuty(x))
            .OrderBy(x => IsRemovableTechnicalAssignment(x) ? 0 : 1)
            .ThenBy(TechnicalPriority)
            .ThenBy(x => x.DriverId);
    }

    private bool CanAssign(
        Driver driver,
        PlanningDuty duty,
        DateOnly date,
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        IReadOnlyCollection<PlanningAssignment> removed,
        IReadOnlyCollection<PlanningAssignment> added,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options)
    {
        if (!PlanningWorkInterval.TryCreate(date, duty, out var interval))
        {
            return false;
        }

        var simulated = ApplySimulation(assignmentContext, removed, added);
        var candidateWorkMinutes = PlanningWorkloadCalculator.ResolveDutyWorkMinutes(duty, date, interval).WorkMinutes;
        var evaluation = _eligibilityChecker.Evaluate(
            driver,
            duty,
            date,
            interval,
            candidateWorkMinutes,
            simulated,
            availabilities,
            options,
            dateFrom,
            dateTo,
            _calendarService);
        return evaluation.IsEligible;
    }

    private static bool ValidateWholePlan(
        PlanningSwapPlan plan,
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options)
    {
        var simulated = ApplySimulation(assignmentContext, plan.RemovedAssignments, plan.AddedAssignments);
        foreach (var group in simulated.Where(PlanningWorkloadCalculator.IsWorkAssignment).GroupBy(x => x.DriverId))
        {
            var intervals = group
                .Select(PlanningWorkloadCalculator.ResolveAssignmentInterval)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .OrderBy(x => x.Start)
                .ToList();
            for (var index = 1; index < intervals.Count; index++)
            {
                if (intervals[index - 1].End > intervals[index].Start)
                {
                    return false;
                }

                if ((intervals[index].Start - intervals[index - 1].End).TotalMinutes < options.MinDailyRestMinutes)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<PlanningAssignment> ApplySimulation(
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningAssignment> removed,
        IReadOnlyCollection<PlanningAssignment> added)
    {
        var removedIds = removed.Select(x => x.Id).ToHashSet();
        return assignmentContext.Where(x => !removedIds.Contains(x.Id)).Concat(added).ToList();
    }

    private static void ApplyPlan(PlanningSwapPlan plan, List<PlanningAssignment> assignmentContext)
    {
        var removedIds = plan.RemovedAssignments.Select(x => x.Id).ToHashSet();
        assignmentContext.RemoveAll(x => removedIds.Contains(x.Id));
        assignmentContext.AddRange(plan.AddedAssignments);
    }

    private static void RollbackPlanTail(PlanningSwapPlan plan, int removedCount, int addedCount, int stepsCount)
    {
        if (plan.RemovedAssignments.Count > removedCount)
        {
            plan.RemovedAssignments.RemoveRange(removedCount, plan.RemovedAssignments.Count - removedCount);
        }

        if (plan.AddedAssignments.Count > addedCount)
        {
            plan.AddedAssignments.RemoveRange(addedCount, plan.AddedAssignments.Count - addedCount);
        }

        if (plan.Steps.Count > stepsCount)
        {
            plan.Steps.RemoveRange(stepsCount, plan.Steps.Count - stepsCount);
        }
    }

    private static IEnumerable<PlanningAssignment> RemovableTechnicalAssignments(IEnumerable<PlanningAssignment> assignments, DateOnly date) =>
        assignments.Where(x => x.Date == date && IsRemovableTechnicalAssignment(x));

    private static bool IsRemovableTechnicalAssignment(PlanningAssignment assignment)
    {
        if (assignment.Status == PlanningAssignmentStatus.Manual)
        {
            return false;
        }

        return PlanningEntryClassifier.Classify(assignment).Kind is PlanningEntryKind.WeeklyDayOff
            or PlanningEntryKind.DayOff
            or PlanningEntryKind.ReserveFirstShift
            or PlanningEntryKind.ReserveSecondShift;
    }

    private static bool IsMovableGeneratedRealDuty(PlanningAssignment assignment)
    {
        if (assignment.Status == PlanningAssignmentStatus.Manual || assignment.PlanningDuty is null)
        {
            return false;
        }

        var kind = PlanningEntryClassifier.Classify(assignment).Kind;
        return kind == PlanningEntryKind.Duty;
    }

    private static int TechnicalPriority(PlanningAssignment assignment) => PlanningEntryClassifier.Classify(assignment).Kind switch
    {
        PlanningEntryKind.WeeklyDayOff => 0,
        PlanningEntryKind.DayOff => 1,
        PlanningEntryKind.ReserveFirstShift => 2,
        PlanningEntryKind.ReserveSecondShift => 3,
        _ => 9
    };

    private static PlanningAssignment CreateGeneratedAssignment(
        Guid companyId,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        Guid driverId,
        PlanningDuty duty,
        DateOnly date,
        DateTime now,
        string note)
    {
        PlanningWorkInterval.TryCreate(date, duty, out var interval);
        var schedule = schedules[(date.Year, date.Month)];
        return new PlanningAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanningScheduleId = schedule.Id,
            DriverId = driverId,
            PlanningDutyId = duty.Id,
            PlanningDuty = duty,
            Date = date,
            StartDateTime = interval.Start,
            EndDateTime = interval.End,
            Status = PlanningAssignmentStatus.Generated,
            AssignmentType = PlanningAssignmentType.Duty,
            Notes = note,
            CreatedAt = now,
            CreatedUtc = now
        };
    }
}

public class PlanningSwapRescueResult
{
    public List<PlanningSwapPlan> Plans { get; } = new();

    public HashSet<(Guid DutyId, DateOnly Date)> ResolvedUnassignedDutyIds { get; } = new();

    public int RescuedDutyCount { get; set; }

    public int DirectSwapCount { get; set; }

    public int ChainSwapCount { get; set; }

    public int RemovedWeeklyDayOffCount { get; set; }

    public int RemovedDayOffCount { get; set; }

    public int RemovedReserveFirstShiftCount { get; set; }

    public int RemovedReserveSecondShiftCount { get; set; }
}

public class PlanningSwapPlan
{
    public bool IsChain { get; set; }

    public List<PlanningAssignment> RemovedAssignments { get; } = new();

    public List<PlanningAssignment> AddedAssignments { get; } = new();

    public List<PlanningSwapStep> Steps { get; } = new();
}

public record PlanningSwapStep(Guid DriverId, Guid? FromDutyId, Guid? ToDutyId, DateOnly Date, string Kind);
