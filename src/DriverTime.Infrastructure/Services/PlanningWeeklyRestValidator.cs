using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public class PlanningWeeklyRestValidator
{
    public PlanningWeeklyRestEvaluation EvaluateCandidate(
        Guid driverId,
        PlanningWorkInterval candidateInterval,
        IReadOnlyCollection<PlanningAssignment> driverAssignments,
        PlanningGenerationOptions options)
    {
        var previous = driverAssignments
            .Where(x => PlanningWorkloadCalculator.IsWorkAssignment(x))
            .Select(x => new AssignmentRestEndpoint(x, PlanningWorkloadCalculator.ResolveAssignmentInterval(x)))
            .Where(x => x.Interval is not null && x.Interval.Value.End <= candidateInterval.Start)
            .OrderByDescending(x => x.Interval!.Value.End)
            .FirstOrDefault();

        var next = driverAssignments
            .Where(x => PlanningWorkloadCalculator.IsWorkAssignment(x))
            .Select(x => new AssignmentRestEndpoint(x, PlanningWorkloadCalculator.ResolveAssignmentInterval(x)))
            .Where(x => x.Interval is not null && x.Interval.Value.Start >= candidateInterval.End)
            .OrderBy(x => x.Interval!.Value.Start)
            .FirstOrDefault();

        var before = previous is null ? null : (int?)(candidateInterval.Start - previous.Interval!.Value.End).TotalMinutes;
        var after = next is null ? null : (int?)(next.Interval!.Value.Start - candidateInterval.End).TotalMinutes;
        var existingGap = previous is not null && next is not null
            ? (int?)(next.Interval!.Value.Start - previous.Interval!.Value.End).TotalMinutes
            : null;

        var bestRemainingRest = new[] { before, after }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .DefaultIfEmpty(int.MaxValue)
            .Max();
        var kind = ClassifyRest(bestRemainingRest == int.MaxValue ? null : bestRemainingRest, options);
        var isClearlyBroken = existingGap >= options.MinWeeklyRestMinutes && bestRemainingRest < options.MinWeeklyRestMinutes;
        var warning = kind is PlanningWeeklyRestKind.Reduced or PlanningWeeklyRestKind.PreferredReduced or PlanningWeeklyRestKind.Insufficient
            ? PlanningCandidateRejectionReasonDescriptions.ToPolishWeeklyRestWarning(kind)
            : null;

        return new PlanningWeeklyRestEvaluation
        {
            DriverId = driverId,
            PeriodStart = previous?.Interval?.End,
            PeriodEnd = next?.Interval?.Start,
            RestMinutes = bestRemainingRest == int.MaxValue ? null : bestRemainingRest,
            RestKind = kind,
            IsEligible = !isClearlyBroken,
            Warning = warning,
            PreviousAssignmentId = previous?.Assignment.Id,
            NextAssignmentId = next?.Assignment.Id,
            RestMinutesBeforeCandidate = before,
            RestMinutesAfterCandidate = after
        };
    }

    public IReadOnlyCollection<PlanningWeeklyRestEvaluation> EvaluateObservedRests(
        Guid driverId,
        IReadOnlyCollection<PlanningAssignment> assignments,
        PlanningGenerationOptions options)
    {
        var endpoints = assignments
            .Where(x => x.DriverId == driverId && PlanningWorkloadCalculator.IsWorkAssignment(x))
            .Select(x => new AssignmentRestEndpoint(x, PlanningWorkloadCalculator.ResolveAssignmentInterval(x)))
            .Where(x => x.Interval is not null)
            .OrderBy(x => x.Interval!.Value.Start)
            .ToList();
        var result = new List<PlanningWeeklyRestEvaluation>();
        for (var index = 1; index < endpoints.Count; index++)
        {
            var previous = endpoints[index - 1];
            var next = endpoints[index];
            var restMinutes = (int)(next.Interval!.Value.Start - previous.Interval!.Value.End).TotalMinutes;
            if (restMinutes < 0)
            {
                continue;
            }

            var kind = ClassifyRest(restMinutes, options);
            result.Add(new PlanningWeeklyRestEvaluation
            {
                DriverId = driverId,
                PeriodStart = previous.Interval.Value.End,
                PeriodEnd = next.Interval.Value.Start,
                RestMinutes = restMinutes,
                RestKind = kind,
                IsEligible = kind != PlanningWeeklyRestKind.Insufficient,
                Warning = kind is PlanningWeeklyRestKind.Reduced or PlanningWeeklyRestKind.PreferredReduced or PlanningWeeklyRestKind.Insufficient
                    ? PlanningCandidateRejectionReasonDescriptions.ToPolishWeeklyRestWarning(kind)
                    : null,
                PreviousAssignmentId = previous.Assignment.Id,
                NextAssignmentId = next.Assignment.Id
            });
        }

        return result;
    }

    public static PlanningWeeklyRestKind ClassifyRest(int? restMinutes, PlanningGenerationOptions options)
    {
        if (!restMinutes.HasValue)
        {
            return PlanningWeeklyRestKind.None;
        }

        if (restMinutes.Value < options.MinWeeklyRestMinutes)
        {
            return PlanningWeeklyRestKind.Insufficient;
        }

        if (restMinutes.Value >= options.RegularWeeklyRestMinutes)
        {
            return PlanningWeeklyRestKind.Regular;
        }

        if (options.PreferredWeeklyRestMinutes.HasValue && restMinutes.Value >= options.PreferredWeeklyRestMinutes.Value)
        {
            return PlanningWeeklyRestKind.PreferredReduced;
        }

        return PlanningWeeklyRestKind.Reduced;
    }

    private sealed record AssignmentRestEndpoint(PlanningAssignment Assignment, PlanningWorkInterval? Interval);
}
