using System.Globalization;
using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public static class PlanningWorkloadCalculator
{
    public static bool IsWorkAssignment(PlanningAssignment assignment) =>
        PlanningEntryClassifier.Classify(assignment).IsRealWork;

    public static bool CountsTowardMonthlyNorm(PlanningAssignment assignment) =>
        PlanningEntryClassifier.Classify(assignment).CountsTowardMonthlyNorm;

    public static int ResolveAssignmentWorkMinutes(PlanningAssignment assignment)
    {
        var classification = PlanningEntryClassifier.Classify(assignment);
        if (!classification.CountsTowardWorkMinutes)
        {
            return 0;
        }

        if (assignment.PlanningDuty is not null)
        {
            var interval = AssignmentIntervalOrDutyInterval(assignment, assignment.PlanningDuty);
            if (interval is not null)
            {
                return PlanningDutyWorkMinutesResolver.Resolve(assignment.PlanningDuty, interval.Value).WorkMinutes;
            }
        }

        if (assignment.StartDateTime.HasValue
            && assignment.EndDateTime.HasValue
            && assignment.EndDateTime.Value > assignment.StartDateTime.Value)
        {
            return (int)(assignment.EndDateTime.Value - assignment.StartDateTime.Value).TotalMinutes;
        }

        return 0;
    }

    public static PlanningDutyWorkMinutesResult ResolveDutyWorkMinutes(PlanningDuty duty, DateOnly date, PlanningWorkInterval interval) =>
        PlanningDutyWorkMinutesResolver.Resolve(duty, interval);

    public static int WorkAssignmentCount(
        Guid driverId,
        IEnumerable<PlanningAssignment> assignments,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options) =>
        assignments.Count(x => x.DriverId == driverId
            && x.Date >= dateFrom
            && x.Date <= dateTo
            && IncludeInWorkload(x, options)
            && IsWorkAssignment(x));

    public static int WorkMinutes(
        Guid driverId,
        IEnumerable<PlanningAssignment> assignments,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options) =>
        RealWorkMinutes(driverId, assignments, dateFrom, dateTo, options);

    public static int RealWorkMinutes(
        Guid driverId,
        IEnumerable<PlanningAssignment> assignments,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options) =>
        assignments
            .Where(x => x.DriverId == driverId
                && x.Date >= dateFrom
                && x.Date <= dateTo
                && IncludeInWorkload(x, options))
            .Sum(ResolveAssignmentWorkMinutes);

    public static int CreditedAbsenceMinutes(
        Guid driverId,
        IEnumerable<PlanningDriverAvailability> availabilities,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningWorkingTimeCalendarService calendarService) =>
        availabilities
            .Where(x => x.DriverId == driverId && x.Type == PlanningDriverAvailabilityType.Vacation)
            .SelectMany(x => EachDate(Max(dateFrom, x.DateFrom), Min(dateTo, x.DateTo)))
            .Distinct()
            .Where(calendarService.IsStandardWorkingDay)
            .Count() * 480;

    public static PlanningDriverWorkloadSummary WorkloadSummary(
        Guid driverId,
        IEnumerable<PlanningAssignment> assignments,
        IEnumerable<PlanningDriverAvailability> availabilities,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        PlanningWorkingTimeCalendarService calendarService)
    {
        var real = RealWorkMinutes(driverId, assignments, dateFrom, dateTo, options);
        var credited = CreditedAbsenceMinutes(driverId, availabilities, dateFrom, dateTo, calendarService);
        return new PlanningDriverWorkloadSummary
        {
            RealWorkMinutes = real,
            CreditedAbsenceMinutes = credited
        };
    }

    public static int WeeklyWorkMinutes(
        Guid driverId,
        IEnumerable<PlanningAssignment> assignments,
        DateOnly date,
        PlanningGenerationOptions options)
    {
        var key = IsoWeekKey.FromDate(date);
        return assignments
            .Where(x => x.DriverId == driverId
                && IncludeInWorkload(x, options)
                && IsoWeekKey.FromDate(x.Date).Equals(key))
            .Sum(ResolveAssignmentWorkMinutes);
    }

    public static int MaxWeeklyWorkMinutesObserved(
        Guid driverId,
        IEnumerable<PlanningAssignment> assignments,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options) =>
        assignments
            .Where(x => x.DriverId == driverId
                && x.Date >= dateFrom
                && x.Date <= dateTo
                && IncludeInWorkload(x, options))
            .GroupBy(x => IsoWeekKey.FromDate(x.Date))
            .Select(x => x.Sum(ResolveAssignmentWorkMinutes))
            .DefaultIfEmpty(0)
            .Max();

    public static int ConsecutiveWorkDaysBefore(Guid driverId, IEnumerable<PlanningAssignment> assignments, DateOnly candidateDate)
    {
        var workDates = WorkDates(driverId, assignments);
        var cursor = candidateDate.AddDays(-1);
        var count = 0;
        while (workDates.Contains(cursor))
        {
            count++;
            cursor = cursor.AddDays(-1);
        }

        return count;
    }

    public static int ConsecutiveWorkDaysAfter(Guid driverId, IEnumerable<PlanningAssignment> assignments, DateOnly candidateDate)
    {
        var workDates = WorkDates(driverId, assignments);
        workDates.Add(candidateDate);
        return ConsecutiveRunLength(workDates, candidateDate);
    }

    public static int MaxConsecutiveWorkDaysObserved(Guid driverId, IEnumerable<PlanningAssignment> assignments)
    {
        var workDates = WorkDates(driverId, assignments).OrderBy(x => x).ToList();
        if (workDates.Count == 0)
        {
            return 0;
        }

        var best = 1;
        var current = 1;
        for (var index = 1; index < workDates.Count; index++)
        {
            current = workDates[index - 1].AddDays(1) == workDates[index] ? current + 1 : 1;
            best = Math.Max(best, current);
        }

        return best;
    }

    public static bool IncludeInWorkload(PlanningAssignment assignment, PlanningGenerationOptions options) =>
        assignment.Status != PlanningAssignmentStatus.Manual || options.IncludeManualAssignmentsInWorkload;

    public static IsoWeekKey GetIsoWeekKey(DateOnly date) => IsoWeekKey.FromDate(date);

    public static List<PlanningWorkInterval> RealWorkIntervals(Guid driverId, IEnumerable<PlanningAssignment> assignments) =>
        assignments
            .Where(x => x.DriverId == driverId && IsWorkAssignment(x))
            .Select(ResolveAssignmentInterval)
            .Where(x => x is not null)
            .Select(x => x!.Value)
            .OrderBy(x => x.Start)
            .ToList();

    public static PlanningWorkInterval? ResolveAssignmentInterval(PlanningAssignment assignment)
    {
        if (assignment.StartDateTime.HasValue
            && assignment.EndDateTime.HasValue
            && assignment.EndDateTime.Value > assignment.StartDateTime.Value)
        {
            return new PlanningWorkInterval(
                assignment.StartDateTime.Value,
                assignment.EndDateTime.Value,
                (int)(assignment.EndDateTime.Value - assignment.StartDateTime.Value).TotalMinutes,
                assignment.EndDateTime.Value.Date > assignment.StartDateTime.Value.Date);
        }

        return assignment.PlanningDuty is not null && PlanningWorkInterval.TryCreate(assignment.Date, assignment.PlanningDuty, out var interval)
            ? interval
            : null;
    }

    private static HashSet<DateOnly> WorkDates(Guid driverId, IEnumerable<PlanningAssignment> assignments) =>
        assignments
            .Where(x => x.DriverId == driverId && IsWorkAssignment(x))
            .Select(x => x.Date)
            .ToHashSet();

    private static int ConsecutiveRunLength(HashSet<DateOnly> workDates, DateOnly anchorDate)
    {
        var runStart = anchorDate;
        while (workDates.Contains(runStart.AddDays(-1)))
        {
            runStart = runStart.AddDays(-1);
        }

        var runEnd = anchorDate;
        while (workDates.Contains(runEnd.AddDays(1)))
        {
            runEnd = runEnd.AddDays(1);
        }

        return runEnd.DayNumber - runStart.DayNumber + 1;
    }

    private static PlanningWorkInterval? AssignmentIntervalOrDutyInterval(PlanningAssignment assignment, PlanningDuty duty) =>
        ResolveAssignmentInterval(assignment) ?? (PlanningWorkInterval.TryCreate(assignment.Date, duty, out var interval) ? interval : null);

    private static IEnumerable<DateOnly> EachDate(DateOnly dateFrom, DateOnly dateTo)
    {
        if (dateFrom > dateTo)
        {
            yield break;
        }

        for (var date = dateFrom; date <= dateTo; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;

    public readonly record struct IsoWeekKey(int Year, int Week)
    {
        public static IsoWeekKey FromDate(DateOnly date)
        {
            var dateTime = date.ToDateTime(TimeOnly.MinValue);
            return new IsoWeekKey(ISOWeek.GetYear(dateTime), ISOWeek.GetWeekOfYear(dateTime));
        }
    }
}
