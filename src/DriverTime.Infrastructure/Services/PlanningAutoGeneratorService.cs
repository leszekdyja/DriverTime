using System.Diagnostics;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Services;

public class PlanningAutoGeneratorService : IPlanningAutoGeneratorService
{
    private const string AutoSchedulePrefix = "Plan automatyczny";

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanningCandidateEvaluator _candidateEvaluator;
    private readonly PlanningWorkingTimeCalendarService _calendarService;
    private readonly PlanningWeeklyRestValidator _weeklyRestValidator = new();
    private readonly PlanningTechnicalAssignmentPlanner _technicalPlanner;
    private readonly PlanningAssignmentSwapPlanner _swapPlanner;
    private readonly ILogger<PlanningAutoGeneratorService>? _logger;

    public PlanningAutoGeneratorService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<PlanningAutoGeneratorService>? logger = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
        _candidateEvaluator = new PlanningCandidateEvaluator(new PlanningEligibilityChecker());
        _calendarService = new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider());
        _technicalPlanner = new PlanningTechnicalAssignmentPlanner(_candidateEvaluator, _calendarService);
        _swapPlanner = new PlanningAssignmentSwapPlanner(new PlanningEligibilityChecker(), _calendarService);
    }

    public Task<PlanningAutoGenerateResultDto> GenerateAsync(
        PlanningAutoGenerateRequestDto request,
        CancellationToken cancellationToken = default) =>
        GenerateInternalAsync(request, isPreview: false, cancellationToken);

    public Task<PlanningAutoGenerateResultDto> PreviewAsync(
        PlanningAutoGenerateRequestDto request,
        CancellationToken cancellationToken = default) =>
        GenerateInternalAsync(request, isPreview: true, cancellationToken);

    private async Task<PlanningAutoGenerateResultDto> GenerateInternalAsync(
        PlanningAutoGenerateRequestDto request,
        bool isPreview,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var calendarStopwatch = Stopwatch.StartNew();
        var baseOptions = BuildOptions(request);
        var monthlyCalendars = EachMonth(request.DateFrom, request.DateTo)
            .Select(x => _calendarService.BuildMonthlyCalendar(x.Year, x.Month))
            .ToList();
        var options = ResolveMonthlyTargetOptions(request, baseOptions, monthlyCalendars);
        ValidateRequest(request, options);

        var companyId = _currentUser.CompanyId;
        options = options with { AssignmentRules = await LoadMergedAssignmentRulesAsync(options.AssignmentRules, companyId, cancellationToken) };
        var now = DateTime.UtcNow;
        var result = new PlanningAutoGenerateResultDto
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            IsPreview = isPreview,
            MonthlyCalendars = monthlyCalendars.Select(ToDto).ToList()
        };
        void RecordTiming(string stage, long elapsedMilliseconds) => AddTiming(result, stage, elapsedMilliseconds);
        RecordTiming("Budowa kalendarza", calendarStopwatch.ElapsedMilliseconds);
        var dataStopwatch = Stopwatch.StartNew();
        var generationWarnings = new HashSet<string>();
        var forbiddenCandidateRejectionCount = 0;

        var requestedDriverIds = request.DriverIds.Where(x => x != Guid.Empty).Distinct().ToList();
        var driversQuery = _dbContext.Drivers.Where(x => x.CompanyId == companyId && x.IncludeInPlanning);
        if (requestedDriverIds.Count > 0)
        {
            driversQuery = driversQuery.Where(x => requestedDriverIds.Contains(x.Id));
        }

        var drivers = await driversQuery
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.CardNumber)
            .ToListAsync(cancellationToken);

        var allDuties = await _dbContext.PlanningDuties
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.DutyNumber)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var duties = allDuties
            .Where(x => PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.Duty)
            .ToList();

        if (drivers.Count == 0)
        {
            result.Messages.Add(requestedDriverIds.Count > 0
                ? "Brak kierowców z requestu aktywnych do planowania w aktualnej firmie."
                : "Brak kierowców aktywnych do planowania w aktualnej firmie.");
            return result;
        }

        if (duties.Count == 0)
        {
            result.Messages.Add("Brak zwykłych służb do zaplanowania automatycznie.");
            return result;
        }

        var schedules = await EnsureSchedulesAsync(companyId, request.DateFrom, request.DateTo, now, cancellationToken);

        var oldGenerated = await _dbContext.PlanningAssignments
            .Where(x => x.CompanyId == companyId
                && x.Date >= request.DateFrom
                && x.Date <= request.DateTo
                && x.Status == PlanningAssignmentStatus.Generated)
            .ToListAsync(cancellationToken);
        _dbContext.PlanningAssignments.RemoveRange(oldGenerated);

        var contextDateFrom = options.IncludeAssignmentsOutsideGeneratedRangeForRestChecks
            ? request.DateFrom.AddDays(-Math.Max(8, options.MaxConsecutiveWorkDays + 2))
            : request.DateFrom;
        var contextDateTo = options.IncludeAssignmentsOutsideGeneratedRangeForRestChecks
            ? request.DateTo.AddDays(8)
            : request.DateTo;

        // Pobieramy jeden bufor kontekstowy dla odpoczynku dobowego, kolejnych dni pracy,
        // tygodniowego odpoczynku i sąsiednich miesięcy. Dalej wszystkie oceny kandydatów
        // działają w pamięci, bez zapytań EF per dzień, kierowca lub kandydat.
        var assignmentContext = await _dbContext.PlanningAssignments
            .Include(x => x.PlanningDuty)
            .Where(x => x.CompanyId == companyId
                && x.Date >= contextDateFrom
                && x.Date <= contextDateTo
                && !(x.Date >= request.DateFrom
                    && x.Date <= request.DateTo
                    && x.Status == PlanningAssignmentStatus.Generated))
            .ToListAsync(cancellationToken);

        var initialAssignmentContext = assignmentContext.ToList();
        var driverIds = drivers.Select(driver => driver.Id).ToList();
        var availabilities = await _dbContext.PlanningDriverAvailabilities
            .Where(x => x.CompanyId == companyId
                && driverIds.Contains(x.DriverId)
                && x.DateFrom <= contextDateTo
                && x.DateTo >= contextDateFrom)
            .ToListAsync(cancellationToken);

        RecordTiming("Pobranie danych", dataStopwatch.ElapsedMilliseconds);

        result.ManualAssignmentsPreserved = assignmentContext.Count(x =>
            x.Status == PlanningAssignmentStatus.Manual
            && x.Date >= request.DateFrom
            && x.Date <= request.DateTo);

        var generatedAssignments = new List<PlanningAssignment>();
        var technicalStopwatch = Stopwatch.StartNew();
        var nightDutyPlan = _technicalPlanner.PlanNightDuties(
            drivers,
            allDuties,
            schedules,
            assignmentContext,
            availabilities,
            request.DateFrom,
            request.DateTo,
            options,
            companyId,
            now);
        AddTechnicalPlanResult(nightDutyPlan, generatedAssignments, result.UnassignedDuties, result.Warnings, _dbContext);
        result.NightDutyGeneratedCount = nightDutyPlan.Assignments.Count;
        result.GeneratedCount += nightDutyPlan.Assignments.Count;

        RecordTiming("Automatyczne RN", technicalStopwatch.ElapsedMilliseconds);

        var assignmentsByDriver = BuildAssignmentsByDriver(assignmentContext);
        var availabilitiesByDriver = BuildAvailabilitiesByDriver(availabilities);

        var evaluationStopwatch = Stopwatch.StartNew();
        var evaluatedCandidateCount = 0;

        var holidayDates = monthlyCalendars
            .SelectMany(x => x.Holidays)
            .Select(x => x.Date)
            .ToHashSet();

        foreach (var date in EachDate(request.DateFrom, request.DateTo))
        {
            var schedule = schedules[(date.Year, date.Month)];
            var dutiesForDate = duties.Where(duty => PlanningDutyDayAvailability.IsActiveOn(duty, date, holidayDates.Contains(date))).ToList();
            foreach (var duty in dutiesForDate)
            {
                var evaluations = _candidateEvaluator.EvaluateCandidates(
                    drivers,
                    duty,
                    date,
                    assignmentsByDriver,
                    availabilitiesByDriver,
                    request.DateFrom,
                    request.DateTo,
                    options,
                    _calendarService);
                evaluatedCandidateCount += evaluations.Count;
                forbiddenCandidateRejectionCount += evaluations.Count(x => x.RejectionReasons.Contains(PlanningCandidateRejectionReason.DriverDutyForbidden));
                var selected = _candidateEvaluator.ChooseBestCandidate(evaluations);

                if (selected is null || !PlanningWorkInterval.TryCreate(date, duty, out var interval))
                {
                    result.UnassignedDuties.Add(CreateUnassignedDuty(duty, date, evaluations));
                    continue;
                }

                var workMinutesResult = PlanningWorkloadCalculator.ResolveDutyWorkMinutes(duty, date, interval);
                if (!string.IsNullOrWhiteSpace(workMinutesResult.Warning))
                {
                    generationWarnings.Add(workMinutesResult.Warning);
                }

                var assignment = new PlanningAssignment
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    PlanningScheduleId = schedule.Id,
                    DriverId = selected.DriverId,
                    PlanningDutyId = duty.Id,
                    PlanningDuty = duty,
                    Date = date,
                    StartDateTime = interval.Start,
                    EndDateTime = interval.End,
                    Status = PlanningAssignmentStatus.Generated,
                    AssignmentType = PlanningAssignmentType.Duty,
                    Notes = $"Wygenerowano automatycznie. Minuty pracy: {workMinutesResult.WorkMinutes} ({workMinutesResult.Source}).",
                    CreatedAt = now,
                    CreatedUtc = now
                };

                _dbContext.PlanningAssignments.Add(assignment);
                assignmentContext.Add(assignment);
                AddAssignmentToIndex(assignmentsByDriver, assignment);
                generatedAssignments.Add(assignment);
                result.GeneratedCount++;
            }
        }

        RecordTiming($"Ocena kandydatów ({evaluatedCandidateCount})", evaluationStopwatch.ElapsedMilliseconds);

        var rescueStopwatch = Stopwatch.StartNew();
        var swapRescue = _swapPlanner.RescueUnassignedDuties(
            drivers,
            allDuties,
            schedules,
            assignmentContext,
            availabilities,
            result.UnassignedDuties,
            request.DateFrom,
            request.DateTo,
            options,
            companyId,
            now);
        ApplySwapRescueResult(swapRescue, generatedAssignments, result, _dbContext);
        result.GeneratedCount += swapRescue.Plans.Sum(x => x.AddedAssignments.Count) - swapRescue.Plans.Sum(x => x.RemovedAssignments.Count);

        RecordTiming("Mechanizmy ratunkowe", rescueStopwatch.ElapsedMilliseconds);

        var finalTechnicalStopwatch = Stopwatch.StartNew();
        var reservePlan = _technicalPlanner.PlanReserves(
            drivers,
            allDuties,
            schedules,
            assignmentContext,
            availabilities,
            result.UnassignedDuties,
            request.DateFrom,
            request.DateTo,
            options,
            companyId,
            now);
        AddTechnicalPlanResult(reservePlan, generatedAssignments, result.UnassignedDuties, result.Warnings, _dbContext);
        result.ReserveGeneratedCount = reservePlan.Assignments.Count;
        result.GeneratedCount += reservePlan.Assignments.Count;

        var dayOffPlan = _technicalPlanner.PlanFinalDayOffs(
            drivers,
            allDuties,
            schedules,
            assignmentContext,
            availabilities,
            request.DateFrom,
            request.DateTo,
            companyId,
            now);
        AddTechnicalPlanResult(dayOffPlan, generatedAssignments, result.UnassignedDuties, result.Warnings, _dbContext);
        result.DayOffGeneratedCount = dayOffPlan.Assignments.Count;
        result.GeneratedCount += dayOffPlan.Assignments.Count;

        RecordTiming("Rezerwy i dni wolne", finalTechnicalStopwatch.ElapsedMilliseconds);

        var diagnosticsStopwatch = Stopwatch.StartNew();
        AddConstraintDiagnostics(result, drivers, generatedAssignments, allDuties, options, companyId);
        result.ForbiddenCandidateRejectionCount += forbiddenCandidateRejectionCount;
        AddVehicleRequirementWarnings(result, allDuties);
        result.UnassignedCount = result.UnassignedDuties.Count;
        SetRejectionDiagnostics(result);
        if (result.UnassignedCount > 0)
        {
            result.Warnings.Add($"Nie obsadzono {result.UnassignedCount} służb. Szczegóły są dostępne w UnassignedDuties.");
        }
        result.Warnings.AddRange(generationWarnings.OrderBy(x => x));
        RecordTiming("Diagnostyka ograniczeń", diagnosticsStopwatch.ElapsedMilliseconds);

        var restSummaryStopwatch = Stopwatch.StartNew();
        result.DriverSummaries = BuildDriverSummaries(
            drivers,
            initialAssignmentContext,
            assignmentContext,
            generatedAssignments,
            request.DateFrom,
            request.DateTo,
            options,
            _calendarService,
            availabilities,
            monthlyCalendars,
            _weeklyRestValidator);

        RecordTiming("Walidacja odpoczynku i podsumowania", restSummaryStopwatch.ElapsedMilliseconds);

        result.ProposedAssignments = BuildProposedAssignments(generatedAssignments, drivers);

        var saveStopwatch = Stopwatch.StartNew();
        if (isPreview)
        {
            _dbContext.ChangeTracker.Clear();
            RecordTiming("Podgląd bez zapisu", saveStopwatch.ElapsedMilliseconds);
        }
        else
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            RecordTiming("Zapis przypisań", saveStopwatch.ElapsedMilliseconds);
        }

        RecordTiming("Całkowity czas wykonania", totalStopwatch.ElapsedMilliseconds);
        LogTimings(result, drivers.Count, duties.Count, request.DateFrom, request.DateTo);
        result.Messages.Add(isPreview
            ? $"Przygotowano podgląd {result.GeneratedCount} przypisań w {totalStopwatch.ElapsedMilliseconds} ms. Nie zapisano zmian."
            : $"Wygenerowano {result.GeneratedCount} przypisań automatycznych w {totalStopwatch.ElapsedMilliseconds} ms.");

        return result;
    }

    public async Task<List<PlanningAssignmentListItemDto>> GetAssignmentsAsync(
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        if (dateFrom > dateTo)
        {
            throw new PlanningDutyValidationException(new[] { "Data od nie może być późniejsza niż data do." });
        }

        var companyId = _currentUser.CompanyId;
        return await _dbContext.PlanningAssignments
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.PlanningDuty)
            .Where(x => x.CompanyId == companyId && x.Date >= dateFrom && x.Date <= dateTo)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Driver.LastName)
            .ThenBy(x => x.Driver.FirstName)
            .ThenBy(x => x.StartDateTime)
            .Select(x => new PlanningAssignmentListItemDto
            {
                Id = x.Id,
                WorkDate = x.Date,
                DriverId = x.DriverId,
                DriverFullName = FormatDriverName(x.Driver),
                PlanningDutyId = x.PlanningDutyId,
                DutyNumber = x.PlanningDuty == null ? null : x.PlanningDuty.DutyNumber,
                StartDateTime = x.StartDateTime,
                EndDateTime = x.EndDateTime,
                Status = x.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }


    internal static List<PlanningAssignmentListItemDto> BuildProposedAssignments(
        IEnumerable<PlanningAssignment> assignments,
        IEnumerable<Driver> drivers)
    {
        var driverNames = drivers.ToDictionary(x => x.Id, FormatDriverName);
        return assignments
            .OrderBy(x => x.Date)
            .ThenBy(x => x.StartDateTime)
            .ThenBy(x => x.DriverId)
            .Select(x => new PlanningAssignmentListItemDto
            {
                Id = x.Id,
                WorkDate = x.Date,
                DriverId = x.DriverId,
                DriverFullName = driverNames.GetValueOrDefault(x.DriverId, string.Empty),
                PlanningDutyId = x.PlanningDutyId,
                DutyNumber = x.PlanningDuty?.DutyNumber,
                StartDateTime = x.StartDateTime,
                EndDateTime = x.EndDateTime,
                Status = x.Status.ToString()
            })
            .ToList();
    }

    private static Dictionary<Guid, List<PlanningAssignment>> BuildAssignmentsByDriver(IEnumerable<PlanningAssignment> assignments) =>
        assignments
            .GroupBy(x => x.DriverId)
            .ToDictionary(x => x.Key, x => x.ToList());

    private static Dictionary<Guid, List<PlanningDriverAvailability>> BuildAvailabilitiesByDriver(IEnumerable<PlanningDriverAvailability> availabilities) =>
        availabilities
            .GroupBy(x => x.DriverId)
            .ToDictionary(x => x.Key, x => x.ToList());

    private static void AddAssignmentToIndex(
        IDictionary<Guid, List<PlanningAssignment>> assignmentsByDriver,
        PlanningAssignment assignment)
    {
        if (!assignmentsByDriver.TryGetValue(assignment.DriverId, out var assignments))
        {
            assignments = new List<PlanningAssignment>();
            assignmentsByDriver[assignment.DriverId] = assignments;
        }

        assignments.Add(assignment);
    }

    private static void AddTiming(
        PlanningAutoGenerateResultDto result,
        string stage,
        long elapsedMilliseconds)
    {
        result.Timings.Add(new PlanningGenerationTimingDto
        {
            Stage = stage,
            ElapsedMilliseconds = elapsedMilliseconds
        });
    }

    private void LogTimings(
        PlanningAutoGenerateResultDto result,
        int driverCount,
        int dutyCount,
        DateOnly dateFrom,
        DateOnly dateTo)
    {
        if (_logger is null)
        {
            return;
        }

        _logger.LogInformation(
            "Planning auto-generation completed for {DateFrom}-{DateTo}. Drivers={DriverCount}, Duties={DutyCount}, Generated={GeneratedCount}, Unassigned={UnassignedCount}, Timings={Timings}",
            dateFrom,
            dateTo,
            driverCount,
            dutyCount,
            result.GeneratedCount,
            result.UnassignedCount,
            string.Join("; ", result.Timings.Select(x => $"{x.Stage}: {x.ElapsedMilliseconds} ms")));
    }
    internal static AssignmentInterval? BuildInterval(DateOnly workDate, PlanningDuty duty)
    {
        if (!PlanningWorkInterval.TryCreate(workDate, duty, out var interval))
        {
            return null;
        }

        return new AssignmentInterval(interval.Start, interval.End);
    }

    internal static bool Overlaps(AssignmentInterval left, AssignmentInterval right) =>
        left.Start < right.End && right.Start < left.End;

    internal static PlanningAssignment? GenerateAssignmentForTest(
        IList<Driver> drivers,
        IDictionary<Guid, List<AssignmentInterval>> plannedIntervalsByDriver,
        ISet<(Guid DriverId, DateOnly Date)> occupiedDriverDays,
        PlanningDuty duty,
        PlanningSchedule schedule,
        DateOnly date,
        Guid companyId,
        DateTime now,
        ref int nextDriverIndex)
    {
        var interval = BuildInterval(date, duty);
        if (interval is null)
        {
            return null;
        }

        var driver = FindAvailableDriver(drivers, plannedIntervalsByDriver, occupiedDriverDays, interval.Value, date, ref nextDriverIndex);
        if (driver is null)
        {
            return null;
        }

        if (!plannedIntervalsByDriver.TryGetValue(driver.Id, out var intervals))
        {
            intervals = new List<AssignmentInterval>();
            plannedIntervalsByDriver[driver.Id] = intervals;
        }
        intervals.Add(interval.Value);
        occupiedDriverDays.Add((driver.Id, date));

        return new PlanningAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanningScheduleId = schedule.Id,
            DriverId = driver.Id,
            PlanningDutyId = duty.Id,
            Date = date,
            StartDateTime = interval.Value.Start,
            EndDateTime = interval.Value.End,
            Status = PlanningAssignmentStatus.Generated,
            AssignmentType = PlanningAssignmentType.Duty,
            CreatedAt = now,
            CreatedUtc = now
        };
    }


    internal static bool IsDriverEligibleForAutoPlanning(
        Driver driver,
        Guid companyId,
        IReadOnlyCollection<Guid> requestedDriverIds)
    {
        return driver.CompanyId == companyId
            && driver.IncludeInPlanning
            && (requestedDriverIds.Count == 0 || requestedDriverIds.Contains(driver.Id));
    }
    internal static IReadOnlyCollection<PlanningAssignmentRule> BuildAssignmentRules(PlanningAutoGenerateRequestDto request, Guid companyId)
    {
        var rules = new List<PlanningAssignmentRule>();
        foreach (var item in request.AssignmentRules)
        {
            if (item.DriverId == Guid.Empty || (!item.DutyId.HasValue && string.IsNullOrWhiteSpace(item.DutyNumber)))
            {
                continue;
            }

            var type = string.Equals(item.Type, "Preferred", StringComparison.OrdinalIgnoreCase)
                ? PlanningAssignmentRuleType.Preferred
                : PlanningAssignmentRuleType.Forbidden;

            rules.Add(new PlanningAssignmentRule
            {
                CompanyId = item.CompanyId.GetValueOrDefault(companyId),
                DriverId = item.DriverId,
                DutyId = item.DutyId,
                DutyNumber = item.DutyNumber,
                Type = type,
                DateFrom = item.DateFrom,
                DateTo = item.DateTo,
                Note = item.Note
            });
        }

        return rules;
    }

    private async Task<IReadOnlyCollection<PlanningAssignmentRule>> LoadMergedAssignmentRulesAsync(
        IEnumerable<PlanningAssignmentRule> requestRules,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var persistentRules = await _dbContext.PlanningDriverDutyRules
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new PlanningAssignmentRule
            {
                CompanyId = x.CompanyId,
                DriverId = x.DriverId,
                DutyId = x.PlanningDutyId,
                Type = x.Type == PlanningDriverDutyRuleType.Preferred
                    ? PlanningAssignmentRuleType.Preferred
                    : PlanningAssignmentRuleType.Forbidden,
                DateFrom = x.ValidFrom,
                DateTo = x.ValidTo,
                Note = x.Notes
            })
            .ToListAsync(cancellationToken);

        return persistentRules
            .Concat(requestRules.Select(rule => rule.CompanyId == Guid.Empty ? rule with { CompanyId = companyId } : rule))
            .Where(rule => rule.CompanyId == companyId)
            .GroupBy(rule => new
            {
                rule.CompanyId,
                rule.DriverId,
                DutyId = rule.DutyId ?? Guid.Empty,
                DutyNumber = PlanningAssignmentConstraintEvaluator.NormalizeDutyNumber(rule.DutyNumber),
                rule.Type,
                rule.DateFrom,
                rule.DateTo
            })
            .Select(group => group.First())
            .ToList();
    }

    private static void AddConstraintDiagnostics(
        PlanningAutoGenerateResultDto result,
        IReadOnlyCollection<Driver> drivers,
        IReadOnlyCollection<PlanningAssignment> generatedAssignments,
        IReadOnlyCollection<PlanningDuty> duties,
        PlanningGenerationOptions options,
        Guid companyId)
    {
        var driversById = drivers.ToDictionary(x => x.Id);
        var dutiesById = duties.ToDictionary(x => x.Id);
        foreach (var assignment in generatedAssignments.Where(x => x.PlanningDutyId.HasValue))
        {
            if (!driversById.TryGetValue(assignment.DriverId, out var driver)
                || !dutiesById.TryGetValue(assignment.PlanningDutyId!.Value, out var duty))
            {
                continue;
            }

            var constraint = PlanningAssignmentConstraintEvaluator.Evaluate(companyId, driver, duty, assignment.Date, options.AssignmentRules);
            if (constraint.IsPreferred)
            {
                result.PreferredAssignmentCount++;
            }
        }

        result.ConstraintBlockedUnassignedCount = result.UnassignedDuties.Count(x =>
            x.CandidateEvaluations.Any(candidate => candidate.RejectionReasons.Contains(PlanningCandidateRejectionReason.DriverDutyForbidden.ToString())));
    }

    private static void AddVehicleRequirementWarnings(
        PlanningAutoGenerateResultDto result,
        IReadOnlyCollection<PlanningDuty> duties)
    {
        var vehicleDutyCount = duties.Count(x => !string.IsNullOrWhiteSpace(x.VehicleRequirement));
        if (vehicleDutyCount == 0)
        {
            return;
        }

        result.VehicleDataWarningCount = vehicleDutyCount;
        result.Warnings.Add($"{vehicleDutyCount} służb ma wymaganie pojazdu, ale DriverTime nie ma jeszcze danych pojazd-kierowca do twardej walidacji; wymaganie pozostaje informacją diagnostyczną.");
    }
    internal static PlanningGenerationOptions BuildOptions(PlanningAutoGenerateRequestDto request) => new()
    {
        MinDailyRestMinutes = request.MinDailyRestMinutes ?? PlanningGenerationOptions.DefaultMinDailyRestMinutes,
        MaxConsecutiveWorkDays = request.MaxConsecutiveWorkDays ?? PlanningGenerationOptions.DefaultMaxConsecutiveWorkDays,
        MaxWeeklyWorkMinutes = request.MaxWeeklyWorkMinutes ?? PlanningGenerationOptions.DefaultMaxWeeklyWorkMinutes,
        TargetMonthlyWorkMinutes = request.TargetMonthlyWorkMinutes,
        TargetMonthlyWorkMinutesSource = request.TargetMonthlyWorkMinutes.HasValue
            ? PlanningMonthlyTargetWorkMinutesSource.Request
            : PlanningMonthlyTargetWorkMinutesSource.None,
        CalculateMonthlyTargetFromCalendar = request.CalculateMonthlyTargetFromCalendar ?? true,
        EnforceMonthlyTargetMaximum = request.EnforceMonthlyTargetMaximum ?? false,
        EnforceWeeklyMaximum = request.EnforceWeeklyMaximum ?? true,
        IncludeManualAssignmentsInWorkload = request.IncludeManualAssignmentsInWorkload ?? true,
        IncludeAssignmentsOutsideGeneratedRangeForRestChecks = request.IncludeAssignmentsOutsideGeneratedRangeForRestChecks ?? true,
        MinWeeklyRestMinutes = request.MinWeeklyRestMinutes ?? PlanningGenerationOptions.DefaultMinWeeklyRestMinutes,
        RegularWeeklyRestMinutes = request.RegularWeeklyRestMinutes ?? PlanningGenerationOptions.DefaultRegularWeeklyRestMinutes,
        PreferredWeeklyRestMinutes = request.PreferredWeeklyRestMinutes ?? PlanningGenerationOptions.DefaultPreferredWeeklyRestMinutes,
        AssignmentRules = BuildAssignmentRules(request, Guid.Empty)
    };

    internal static PlanningGenerationOptions ResolveMonthlyTargetOptions(
        PlanningAutoGenerateRequestDto request,
        PlanningGenerationOptions options,
        IReadOnlyCollection<PlanningMonthlyWorkingTimeCalendar> calendars)
    {
        if (request.TargetMonthlyWorkMinutes.HasValue)
        {
            return options with
            {
                TargetMonthlyWorkMinutes = request.TargetMonthlyWorkMinutes,
                TargetMonthlyWorkMinutesSource = PlanningMonthlyTargetWorkMinutesSource.Request
            };
        }

        if (options.CalculateMonthlyTargetFromCalendar)
        {
            return options with
            {
                TargetMonthlyWorkMinutes = calendars.Sum(x => x.TargetWorkMinutes),
                TargetMonthlyWorkMinutesSource = PlanningMonthlyTargetWorkMinutesSource.Calendar
            };
        }

        return options with
        {
            TargetMonthlyWorkMinutes = null,
            TargetMonthlyWorkMinutesSource = PlanningMonthlyTargetWorkMinutesSource.None
        };
    }


    internal static List<PlanningDriverGenerationSummaryDto> BuildDriverSummaries(
        IReadOnlyCollection<Driver> drivers,
        IReadOnlyCollection<PlanningAssignment> initialAssignments,
        IReadOnlyCollection<PlanningAssignment> finalAssignments,
        IReadOnlyCollection<PlanningAssignment> generatedAssignments,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options) =>
        BuildDriverSummaries(
            drivers,
            initialAssignments,
            finalAssignments,
            generatedAssignments,
            dateFrom,
            dateTo,
            options,
            new PlanningWorkingTimeCalendarService(new PolishPublicHolidayProvider()),
            Array.Empty<PlanningDriverAvailability>(),
            Array.Empty<PlanningMonthlyWorkingTimeCalendar>(),
            new PlanningWeeklyRestValidator());

    internal static List<PlanningDriverGenerationSummaryDto> BuildDriverSummaries(
        IReadOnlyCollection<Driver> drivers,
        IReadOnlyCollection<PlanningAssignment> initialAssignments,
        IReadOnlyCollection<PlanningAssignment> finalAssignments,
        IReadOnlyCollection<PlanningAssignment> generatedAssignments,
        DateOnly dateFrom,
        DateOnly dateTo,
        PlanningGenerationOptions options,
        PlanningWorkingTimeCalendarService calendarService,
        IReadOnlyCollection<PlanningDriverAvailability> availabilities,
        IReadOnlyCollection<PlanningMonthlyWorkingTimeCalendar> calendars,
        PlanningWeeklyRestValidator weeklyRestValidator)
    {
        var summaries = new List<PlanningDriverGenerationSummaryDto>();
        var calendarTarget = calendars.Sum(x => x.TargetWorkMinutes);
        foreach (var driver in drivers.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.CardNumber))
        {
            var before = PlanningWorkloadCalculator.WorkloadSummary(driver.Id, initialAssignments, availabilities, dateFrom, dateTo, options, calendarService);
            var generatedReal = PlanningWorkloadCalculator.RealWorkMinutes(driver.Id, generatedAssignments, dateFrom, dateTo, options);
            var after = PlanningWorkloadCalculator.WorkloadSummary(driver.Id, finalAssignments, availabilities, dateFrom, dateTo, options, calendarService);
            var weeklyRests = weeklyRestValidator.EvaluateObservedRests(driver.Id, finalAssignments, options);
            var summary = new PlanningDriverGenerationSummaryDto
            {
                DriverId = driver.Id,
                DriverName = FormatDriverName(driver),
                ExistingManualAssignments = initialAssignments.Count(x => x.DriverId == driver.Id && x.Status == PlanningAssignmentStatus.Manual && x.Date >= dateFrom && x.Date <= dateTo),
                GeneratedAssignments = generatedAssignments.Count(x => x.DriverId == driver.Id && x.Date >= dateFrom && x.Date <= dateTo),
                TotalAssignments = finalAssignments.Count(x => x.DriverId == driver.Id && x.Date >= dateFrom && x.Date <= dateTo && PlanningWorkloadCalculator.IsWorkAssignment(x)),
                WorkMinutesBefore = before.RealWorkMinutes,
                GeneratedWorkMinutes = generatedReal,
                WorkMinutesAfter = after.RealWorkMinutes,
                RealWorkMinutesBefore = before.RealWorkMinutes,
                CreditedAbsenceMinutesBefore = before.CreditedAbsenceMinutes,
                RealWorkMinutesGenerated = generatedReal,
                CreditedAbsenceMinutesGenerated = 0,
                RealWorkMinutesAfter = after.RealWorkMinutes,
                CreditedAbsenceMinutesAfter = after.CreditedAbsenceMinutes,
                MonthlyNormMinutesAfter = after.MonthlyNormMinutes,
                TargetMonthlyWorkMinutes = options.TargetMonthlyWorkMinutes,
                TargetMonthlyWorkMinutesSource = options.TargetMonthlyWorkMinutesSource.ToString(),
                CalendarTargetWorkMinutes = calendarTarget,
                MonthlyDeficitAfter = options.TargetMonthlyWorkMinutes.HasValue ? options.TargetMonthlyWorkMinutes.Value - after.MonthlyNormMinutes : null,
                MaxWeeklyWorkMinutesObserved = PlanningWorkloadCalculator.MaxWeeklyWorkMinutesObserved(driver.Id, finalAssignments, dateFrom, dateTo, options),
                MaxConsecutiveWorkDaysObserved = PlanningWorkloadCalculator.MaxConsecutiveWorkDaysObserved(driver.Id, finalAssignments),
                ReducedWeeklyRestCount = weeklyRests.Count(x => x.RestKind is PlanningWeeklyRestKind.Reduced or PlanningWeeklyRestKind.PreferredReduced),
                RegularWeeklyRestCount = weeklyRests.Count(x => x.RestKind == PlanningWeeklyRestKind.Regular),
                InsufficientWeeklyRestCount = weeklyRests.Count(x => x.RestKind == PlanningWeeklyRestKind.Insufficient),
                WeeklyRestWarnings = weeklyRests.Where(x => !string.IsNullOrWhiteSpace(x.Warning)).Select(x => x.Warning!).Distinct().ToList()
            };

            if (options.TargetMonthlyWorkMinutes.HasValue && after.MonthlyNormMinutes > options.TargetMonthlyWorkMinutes.Value)
            {
                summary.Warnings.Add("Przekroczono miesięczny target normy czasu pracy.");
            }

            if (summary.MaxWeeklyWorkMinutesObserved > options.MaxWeeklyWorkMinutes)
            {
                summary.Warnings.Add("Przekroczono tygodniowy limit minut pracy.");
            }

            if (summary.MaxConsecutiveWorkDaysObserved > options.MaxConsecutiveWorkDays)
            {
                summary.Warnings.Add("Przekroczono limit kolejnych dni pracy.");
            }

            if (summary.InsufficientWeeklyRestCount > 0)
            {
                summary.Warnings.Add("Występuje niewystarczający odpoczynek tygodniowy.");
            }

            summaries.Add(summary);
        }

        return summaries;
    }

    private static Driver? FindAvailableDriver(
        IList<Driver> drivers,
        IDictionary<Guid, List<AssignmentInterval>> plannedIntervalsByDriver,
        ISet<(Guid DriverId, DateOnly Date)> occupiedDriverDays,
        AssignmentInterval candidateInterval,
        DateOnly workDate,
        ref int nextDriverIndex)
    {
        for (var offset = 0; offset < drivers.Count; offset++)
        {
            var index = (nextDriverIndex + offset) % drivers.Count;
            var driver = drivers[index];
            if (occupiedDriverDays.Contains((driver.Id, workDate)))
            {
                continue;
            }

            if (plannedIntervalsByDriver.TryGetValue(driver.Id, out var intervals)
                && intervals.Any(existing => Overlaps(existing, candidateInterval)))
            {
                continue;
            }

            nextDriverIndex = (index + 1) % drivers.Count;
            return driver;
        }

        return null;
    }

    internal static void SetRejectionDiagnostics(PlanningAutoGenerateResultDto result)
    {
        var rejectedCandidates = result.UnassignedDuties
            .SelectMany(x => x.CandidateEvaluations)
            .Where(x => !x.IsEligible)
            .ToList();

        result.CandidateRejectionCount = rejectedCandidates.Count;
        result.TimeConflictRejectionCount = rejectedCandidates.Count(x => x.RejectionReasons.Contains(PlanningCandidateRejectionReason.OverlappingAssignment.ToString()));
        result.DailyRestRejectionCount = rejectedCandidates.Count(x => x.RejectionReasons.Contains(PlanningCandidateRejectionReason.InsufficientDailyRestBefore.ToString())
            || x.RejectionReasons.Contains(PlanningCandidateRejectionReason.InsufficientDailyRestAfter.ToString()));
        result.WeeklyRestRejectionCount = rejectedCandidates.Count(x => x.RejectionReasons.Contains(PlanningCandidateRejectionReason.InsufficientWeeklyRest.ToString()));

        // Backward-compatible field: now means time conflicts only, not all unassigned duties.
        result.ConflictCount = result.TimeConflictRejectionCount;
    }
    private static PlanningUnassignedDutyDto CreateUnassignedDuty(
        PlanningDuty duty,
        DateOnly date,
        IReadOnlyCollection<PlanningCandidateEvaluation> evaluations)
    {
        DateTime? start = null;
        DateTime? end = null;
        if (PlanningWorkInterval.TryCreate(date, duty, out var interval))
        {
            start = interval.Start;
            end = interval.End;
        }

        var candidateDtos = evaluations
            .OrderBy(x => x.DriverName)
            .ThenBy(x => x.DriverId)
            .Select(PlanningCandidateEvaluationDto.FromEvaluation)
            .ToList();

        return new PlanningUnassignedDutyDto
        {
            DutyId = duty.Id,
            DutyNumber = duty.DutyNumber,
            Date = date,
            StartDateTime = start,
            EndDateTime = end,
            CandidateEvaluations = candidateDtos,
            Summary = BuildUnassignedSummary(evaluations)
        };
    }

    private static string BuildUnassignedSummary(IReadOnlyCollection<PlanningCandidateEvaluation> evaluations)
    {
        if (evaluations.Count == 0)
        {
            return "Nie znaleziono kierowcy. Brak kandydatów do oceny.";
        }

        var reasonParts = evaluations
            .SelectMany(x => x.RejectionReasons)
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key.ToString())
            .Select(x => $"{x.Count()}: {PlanningCandidateRejectionReasonDescriptions.ToPolishDescription(x.Key)}")
            .ToList();

        return reasonParts.Count == 0
            ? "Nie znaleziono kierowcy mimo braku twardych powodów odrzucenia. Sprawdź dane wejściowe planowania."
            : $"Nie znaleziono kierowcy. {string.Join(" ", reasonParts)}";
    }



    private static void ApplySwapRescueResult(
        PlanningSwapRescueResult rescueResult,
        List<PlanningAssignment> generatedAssignments,
        PlanningAutoGenerateResultDto result,
        DriverTimeDbContext dbContext)
    {
        if (rescueResult.Plans.Count == 0)
        {
            return;
        }

        var removedIds = rescueResult.Plans.SelectMany(x => x.RemovedAssignments).Select(x => x.Id).ToHashSet();
        generatedAssignments.RemoveAll(x => removedIds.Contains(x.Id));
        foreach (var removed in rescueResult.Plans.SelectMany(x => x.RemovedAssignments))
        {
            dbContext.PlanningAssignments.Remove(removed);
        }

        foreach (var added in rescueResult.Plans.SelectMany(x => x.AddedAssignments))
        {
            dbContext.PlanningAssignments.Add(added);
            generatedAssignments.Add(added);
        }

        result.RescueResolvedDutyCount = rescueResult.RescuedDutyCount;
        result.DirectSwapCount = rescueResult.DirectSwapCount;
        result.ChainSwapCount = rescueResult.ChainSwapCount;
        result.RemovedWeeklyDayOffCount = rescueResult.RemovedWeeklyDayOffCount;
        result.RemovedDayOffCount = rescueResult.RemovedDayOffCount;
        result.RemovedReserveFirstShiftCount = rescueResult.RemovedReserveFirstShiftCount;
        result.RemovedReserveSecondShiftCount = rescueResult.RemovedReserveSecondShiftCount;
        result.UnassignedDuties = result.UnassignedDuties
            .Where(x => !rescueResult.ResolvedUnassignedDutyIds.Contains((x.DutyId, x.Date)))
            .ToList();
        result.Warnings.Add($"Ratunek obsadził {rescueResult.RescuedDutyCount} brakujących służb przez {rescueResult.DirectSwapCount} zamian bezpośrednich i {rescueResult.ChainSwapCount} zamian łańcuchowych.");
    }
    private static void AddTechnicalPlanResult(
        PlanningTechnicalAssignmentPlanResult planResult,
        List<PlanningAssignment> generatedAssignments,
        List<PlanningUnassignedDutyDto> unassignedDuties,
        List<string> warnings,
        DriverTimeDbContext dbContext)
    {
        foreach (var assignment in planResult.Assignments)
        {
            dbContext.PlanningAssignments.Add(assignment);
            generatedAssignments.Add(assignment);
        }

        unassignedDuties.AddRange(planResult.UnassignedDuties);
        foreach (var warning in planResult.Warnings.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (!warnings.Contains(warning))
            {
                warnings.Add(warning);
            }
        }
    }
    private async Task<Dictionary<(int Year, int Month), PlanningSchedule>> EnsureSchedulesAsync(
        Guid companyId,
        DateOnly dateFrom,
        DateOnly dateTo,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var months = EachMonth(dateFrom, dateTo).ToList();
        var years = months.Select(x => x.Year).Distinct().ToList();
        var monthNumbers = months.Select(x => x.Month).Distinct().ToList();

        var existing = await _dbContext.PlanningSchedules
            .Where(x => x.CompanyId == companyId && years.Contains(x.Year) && monthNumbers.Contains(x.Month))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<(int Year, int Month), PlanningSchedule>();
        foreach (var month in months)
        {
            var schedule = existing
                .OrderBy(x => x.CreatedUtc)
                .FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month);

            if (schedule is null)
            {
                schedule = new PlanningSchedule
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Name = $"{AutoSchedulePrefix} {month.Year}-{month.Month:00}",
                    Year = month.Year,
                    Month = month.Month,
                    Notes = "Grafik utworzony automatycznie przez generator MVP.",
                    CreatedAt = now,
                    CreatedUtc = now
                };
                _dbContext.PlanningSchedules.Add(schedule);
                existing.Add(schedule);
            }

            result[(month.Year, month.Month)] = schedule;
        }

        return result;
    }

    private static void ValidateRequest(PlanningAutoGenerateRequestDto request, PlanningGenerationOptions options)
    {
        var errors = new List<string>();
        if (request.DateFrom == default)
        {
            errors.Add("Podaj datę od.");
        }

        if (request.DateTo == default)
        {
            errors.Add("Podaj datę do.");
        }

        if (request.DateFrom > request.DateTo)
        {
            errors.Add("Data od nie może być późniejsza niż data do.");
        }

        if (options.MinDailyRestMinutes < 0 || options.MinDailyRestMinutes > 24 * 60)
        {
            errors.Add("Minimalny odpoczynek dobowy musi być w zakresie 0-1440 minut.");
        }

        if (options.MaxConsecutiveWorkDays < 1 || options.MaxConsecutiveWorkDays > 14)
        {
            errors.Add("Maksymalna liczba kolejnych dni pracy musi być w zakresie 1-14.");
        }

        if (options.MaxWeeklyWorkMinutes < 0 || options.MaxWeeklyWorkMinutes > 7 * 24 * 60)
        {
            errors.Add("Tygodniowy limit pracy musi być w zakresie 0-10080 minut.");
        }

        if (options.TargetMonthlyWorkMinutes is < 0 or > 31 * 24 * 60)
        {
            errors.Add("Miesięczny target pracy musi być w zakresie 0-44640 minut.");
        }

        if (options.MinWeeklyRestMinutes < 0 || options.MinWeeklyRestMinutes > 7 * 24 * 60)
        {
            errors.Add("Minimalny odpoczynek tygodniowy musi być w zakresie 0-10080 minut.");
        }

        if (options.RegularWeeklyRestMinutes < options.MinWeeklyRestMinutes || options.RegularWeeklyRestMinutes > 7 * 24 * 60)
        {
            errors.Add("Regularny odpoczynek tygodniowy musi być większy lub równy minimum i nie większy niż 10080 minut.");
        }

        if (options.PreferredWeeklyRestMinutes is < 0 or > 10080)
        {
            errors.Add("Preferowany odpoczynek tygodniowy musi być w zakresie 0-10080 minut.");
        }

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }

    private static PlanningMonthlyWorkingTimeCalendarDto ToDto(PlanningMonthlyWorkingTimeCalendar calendar) => new()
    {
        Year = calendar.Year,
        Month = calendar.Month,
        WeekdayCount = calendar.WeekdayCount,
        HolidayReductionDays = calendar.PublicHolidayReductionDays,
        WorkingDays = calendar.WorkingDays,
        TargetWorkMinutes = calendar.TargetWorkMinutes,
        Holidays = calendar.Holidays.Select(x => new PlanningPublicHolidayDto { Date = x.Date, Name = x.Name }).ToList(),
        StandardWorkingDays = calendar.StandardWorkingDays.ToList()
    };

    private static IEnumerable<DateOnly> EachDate(DateOnly dateFrom, DateOnly dateTo)
    {
        for (var date = dateFrom; date <= dateTo; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static IEnumerable<(int Year, int Month)> EachMonth(DateOnly dateFrom, DateOnly dateTo)
    {
        var cursor = new DateOnly(dateFrom.Year, dateFrom.Month, 1);
        var end = new DateOnly(dateTo.Year, dateTo.Month, 1);
        while (cursor <= end)
        {
            yield return (cursor.Year, cursor.Month);
            cursor = cursor.AddMonths(1);
        }
    }

    private static string FormatDriverName(Driver driver) => PlanningEligibilityChecker.FormatDriverName(driver);
}

public readonly record struct AssignmentInterval(DateTime Start, DateTime End);
