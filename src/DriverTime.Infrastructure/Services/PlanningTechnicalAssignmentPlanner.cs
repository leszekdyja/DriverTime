using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public class PlanningTechnicalAssignmentPlanner
{
    private readonly PlanningCandidateEvaluator _candidateEvaluator;
    private readonly PlanningWorkingTimeCalendarService _calendarService;

    public PlanningTechnicalAssignmentPlanner(
        PlanningCandidateEvaluator candidateEvaluator,
        PlanningWorkingTimeCalendarService calendarService)
    {
        _candidateEvaluator = candidateEvaluator;
        _calendarService = calendarService;
    }

    public PlanningTechnicalAssignmentPlanResult PlanNightDuties(
        IReadOnlyList<Driver> drivers,
        IReadOnlyCollection<PlanningDuty> duties,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        List<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        Guid companyId,
        DateTime now)
    {
        var result = new PlanningTechnicalAssignmentPlanResult();
        var nightDuty = duties.FirstOrDefault(x => PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.NightDuty);
        if (nightDuty is null)
        {
            result.Warnings.Add("Brak służby RN w bibliotece służb. Pominięto automatyczne planowanie RN.");
            return result;
        }

        foreach (var block in BuildNightDutyBlocks(dateFrom, dateTo))
        {
            var required = block.Count == 1 && IsSingleNightDutyDay(block[0]) ? 1 : 2;
            var selectedDrivers = SelectDriversForNightBlock(drivers, nightDuty, block, required, assignmentContext, availabilities, dateFrom, dateTo, options);
            if (selectedDrivers.Count < required)
            {
                foreach (var day in block)
                {
                    result.UnassignedDuties.Add(CreateUnassignedDuty(nightDuty, day, Array.Empty<PlanningCandidateEvaluation>(), "Nie znaleziono pełnej obsady RN dla wymaganego bloku."));
                }
                continue;
            }

            foreach (var day in block)
            {
                var schedule = schedules[(day.Year, day.Month)];
                var dayRequired = NightDutyRequiredCount(day);
                var alreadyPlanned = assignmentContext.Count(x => x.Date == day && PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.NightDuty);
                foreach (var driver in selectedDrivers.Take(Math.Max(0, dayRequired - alreadyPlanned)))
                {
                    if (!PlanningWorkInterval.TryCreate(day, nightDuty, out var interval))
                    {
                        result.UnassignedDuties.Add(CreateUnassignedDuty(nightDuty, day, Array.Empty<PlanningCandidateEvaluation>(), "RN nie ma poprawnych godzin rozpoczęcia i zakończenia."));
                        continue;
                    }

                    var assignment = CreateAssignment(companyId, schedule.Id, driver.Id, nightDuty, day, interval, now, "RN narzucone przed zwykłymi służbami zgodnie ze starym planerem.");
                    assignmentContext.Add(assignment);
                    result.Assignments.Add(assignment);
                }
            }
        }

        return result;
    }

    public PlanningTechnicalAssignmentPlanResult PlanReserves(
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
        var result = new PlanningTechnicalAssignmentPlanResult();
        var reserveDuties = duties
            .Where(x => PlanningEntryClassifier.Classify(x).Kind is PlanningEntryKind.ReserveFirstShift or PlanningEntryKind.ReserveSecondShift)
            .OrderBy(x => PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.ReserveFirstShift ? 0 : 1)
            .ToList();
        if (reserveDuties.Count == 0 || !options.TargetMonthlyWorkMinutes.HasValue)
        {
            return result;
        }

        var blockedDates = unassignedDuties.Select(x => x.Date).ToHashSet();
        foreach (var driver in drivers.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id))
        {
            var guard = 0;
            while (guard++ < 31)
            {
                var workload = PlanningWorkloadCalculator.WorkloadSummary(driver.Id, assignmentContext, availabilities, dateFrom, dateTo, options, _calendarService);
                if (workload.MonthlyNormMinutes >= options.TargetMonthlyWorkMinutes.Value)
                {
                    break;
                }

                var planned = false;
                foreach (var day in EachDate(dateFrom, dateTo))
                {
                    if (blockedDates.Contains(day) || HasAnyAssignment(driver.Id, day, assignmentContext) || HasAvailability(driver.Id, day, availabilities))
                    {
                        continue;
                    }

                    foreach (var reserveDuty in reserveDuties)
                    {
                        var evaluations = _candidateEvaluator.EvaluateCandidates(
                            new[] { driver },
                            reserveDuty,
                            day,
                            assignmentContext,
                            availabilities,
                            dateFrom,
                            dateTo,
                            options,
                            _calendarService);
                        var selected = _candidateEvaluator.ChooseBestCandidate(evaluations);
                        if (selected is null || !PlanningWorkInterval.TryCreate(day, reserveDuty, out var interval))
                        {
                            continue;
                        }

                        var schedule = schedules[(day.Year, day.Month)];
                        var assignment = CreateAssignment(companyId, schedule.Id, driver.Id, reserveDuty, day, interval, now, "Rezerwa R/R2 dopisana po zwykłych służbach do domknięcia RBH.");
                        assignmentContext.Add(assignment);
                        result.Assignments.Add(assignment);
                        planned = true;
                        break;
                    }

                    if (planned)
                    {
                        break;
                    }
                }

                if (!planned)
                {
                    break;
                }
            }
        }

        return result;
    }

    public PlanningTechnicalAssignmentPlanResult PlanFinalDayOffs(
        IReadOnlyList<Driver> drivers,
        IReadOnlyCollection<PlanningDuty> duties,
        IDictionary<(int Year, int Month), PlanningSchedule> schedules,
        List<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        DateOnly dateFrom,
        DateOnly dateTo,
        Guid companyId,
        DateTime now)
    {
        var result = new PlanningTechnicalAssignmentPlanResult();
        var weeklyDayOff = duties.FirstOrDefault(x => PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.WeeklyDayOff);
        var weekendDayOff = duties.FirstOrDefault(x => PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.DayOff);
        if (weeklyDayOff is null && weekendDayOff is null)
        {
            result.Warnings.Add("Brak służb WG/W w bibliotece. Nie uzupełniono pustych komórek dniami wolnymi.");
            return result;
        }

        foreach (var day in EachDate(dateFrom, dateTo))
        {
            var dayOffDuty = IsWeekday(day) ? weeklyDayOff ?? weekendDayOff : weekendDayOff ?? weeklyDayOff;
            if (dayOffDuty is null)
            {
                continue;
            }

            foreach (var driver in drivers)
            {
                if (HasAnyAssignment(driver.Id, day, assignmentContext) || HasAvailability(driver.Id, day, availabilities))
                {
                    continue;
                }

                var schedule = schedules[(day.Year, day.Month)];
                var assignment = new PlanningAssignment
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    PlanningScheduleId = schedule.Id,
                    DriverId = driver.Id,
                    PlanningDutyId = dayOffDuty.Id,
                    PlanningDuty = dayOffDuty,
                    Date = day,
                    Status = PlanningAssignmentStatus.Generated,
                    AssignmentType = PlanningAssignmentType.DayOff,
                    Notes = IsWeekday(day) ? "Techniczne WG dopisane na końcu planowania." : "Techniczne W dopisane na końcu planowania.",
                    CreatedAt = now,
                    CreatedUtc = now
                };
                assignmentContext.Add(assignment);
                result.Assignments.Add(assignment);
            }
        }

        return result;
    }

    public static int NightDutyRequiredCount(DateOnly day) => IsSingleNightDutyDay(day) ? 1 : 2;

    private static bool IsSingleNightDutyDay(DateOnly day) =>
        day.DayOfWeek == DayOfWeek.Saturday || IsPolishHolidayOutsideSunday(day);

    private static bool IsPolishHolidayOutsideSunday(DateOnly day) =>
        day.DayOfWeek != DayOfWeek.Sunday && new PolishPublicHolidayProvider().GetHolidays(day.Year).Any(x => x.Date == day);

    private static List<List<DateOnly>> BuildNightDutyBlocks(DateOnly dateFrom, DateOnly dateTo)
    {
        var blocks = new List<List<DateOnly>>();
        var seen = new HashSet<DateOnly>();
        foreach (var day in EachDate(dateFrom, dateTo))
        {
            if (!seen.Add(day))
            {
                continue;
            }

            if (IsSingleNightDutyDay(day))
            {
                blocks.Add(new List<DateOnly> { day });
                continue;
            }

            var daysSinceSunday = ((int)day.DayOfWeek + 7 - (int)DayOfWeek.Sunday) % 7;
            var start = day.AddDays(-daysSinceSunday);
            var block = new List<DateOnly>();
            for (var offset = 0; offset < 6; offset++)
            {
                var candidate = start.AddDays(offset);
                if (candidate >= dateFrom && candidate <= dateTo && !IsSingleNightDutyDay(candidate))
                {
                    block.Add(candidate);
                    seen.Add(candidate);
                }
            }

            if (block.Count > 0)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    private List<Driver> SelectDriversForNightBlock(
        IReadOnlyList<Driver> drivers,
        PlanningDuty nightDuty,
        IReadOnlyCollection<DateOnly> block,
        int required,
        IReadOnlyCollection<PlanningAssignment> assignmentContext,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options)
    {
        var candidates = new List<(Driver Driver, int Score)>();
        foreach (var driver in drivers)
        {
            var monthRnCount = assignmentContext.Count(x => x.DriverId == driver.Id && PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.NightDuty && x.Date >= dateFrom && x.Date <= dateTo);
            if (block.Any(day => HasAnyAssignment(driver.Id, day, assignmentContext) || HasAvailability(driver.Id, day, availabilities)))
            {
                continue;
            }

            var evaluations = block.Select(day => _candidateEvaluator.EvaluateCandidates(
                    new[] { driver },
                    nightDuty,
                    day,
                    assignmentContext,
                    availabilities,
                    dateFrom,
                    dateTo,
                    options,
                    _calendarService).Single())
                .ToList();
            if (evaluations.Any(x => !x.IsEligible))
            {
                continue;
            }

            var score = monthRnCount * 1000
                + PlanningWorkloadCalculator.WorkAssignmentCount(driver.Id, assignmentContext, dateFrom, dateTo, options) * 20
                + Math.Abs(driver.Id.GetHashCode() % 1000);
            candidates.Add((driver, score));
        }

        return candidates
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Driver.LastName)
            .ThenBy(x => x.Driver.FirstName)
            .ThenBy(x => x.Driver.Id)
            .Take(required)
            .Select(x => x.Driver)
            .ToList();
    }

    private static PlanningAssignment CreateAssignment(
        Guid companyId,
        Guid scheduleId,
        Guid driverId,
        PlanningDuty duty,
        DateOnly date,
        PlanningWorkInterval interval,
        DateTime now,
        string note) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        PlanningScheduleId = scheduleId,
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

    private static PlanningUnassignedDutyDto CreateUnassignedDuty(
        PlanningDuty duty,
        DateOnly date,
        IReadOnlyCollection<PlanningCandidateEvaluation> evaluations,
        string summary)
    {
        DateTime? start = null;
        DateTime? end = null;
        if (PlanningWorkInterval.TryCreate(date, duty, out var interval))
        {
            start = interval.Start;
            end = interval.End;
        }

        return new PlanningUnassignedDutyDto
        {
            DutyId = duty.Id,
            DutyNumber = duty.DutyNumber,
            Date = date,
            StartDateTime = start,
            EndDateTime = end,
            CandidateEvaluations = evaluations.Select(PlanningCandidateEvaluationDto.FromEvaluation).ToList(),
            Summary = summary
        };
    }

    private static bool HasAnyAssignment(Guid driverId, DateOnly day, IEnumerable<PlanningAssignment> assignments) =>
        assignments.Any(x => x.DriverId == driverId && x.Date == day);

    private static bool HasAvailability(Guid driverId, DateOnly day, IEnumerable<PlanningDriverAvailability> availabilities) =>
        availabilities.Any(x => x.DriverId == driverId && x.DateFrom <= day && x.DateTo >= day);

    private static bool IsWeekday(DateOnly day) =>
        day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

    private static IEnumerable<DateOnly> EachDate(DateOnly dateFrom, DateOnly dateTo)
    {
        for (var day = dateFrom; day <= dateTo; day = day.AddDays(1))
        {
            yield return day;
        }
    }
}

public class PlanningTechnicalAssignmentPlanResult
{
    public List<PlanningAssignment> Assignments { get; } = new();

    public List<PlanningUnassignedDutyDto> UnassignedDuties { get; } = new();

    public List<string> Warnings { get; } = new();
}
