using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningAutoGeneratorService : IPlanningAutoGeneratorService
{
    private const string AutoSchedulePrefix = "Plan automatyczny";

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PlanningAutoGeneratorService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<PlanningAutoGenerateResultDto> GenerateAsync(
        PlanningAutoGenerateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var companyId = _currentUser.CompanyId;
        var now = DateTime.UtcNow;
        var result = new PlanningAutoGenerateResultDto
        {
            DateFrom = request.DateFrom,
            DateTo = request.DateTo
        };

        var requestedDriverIds = request.DriverIds.Where(x => x != Guid.Empty).Distinct().ToList();
        var driversQuery = _dbContext.Drivers.Where(x => x.CompanyId == companyId);
        if (requestedDriverIds.Count > 0)
        {
            driversQuery = driversQuery.Where(x => requestedDriverIds.Contains(x.Id));
        }

        var drivers = await driversQuery
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.CardNumber)
            .ToListAsync(cancellationToken);

        var duties = await _dbContext.PlanningDuties
            .Where(x => x.CompanyId == companyId && x.StartTime.HasValue && x.EndTime.HasValue)
            .OrderBy(x => x.DutyNumber)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (drivers.Count == 0)
        {
            result.Messages.Add(requestedDriverIds.Count > 0
                ? "Brak kierowców z requestu w aktualnej firmie."
                : "Brak kierowców w aktualnej firmie.");
            return result;
        }

        if (duties.Count == 0)
        {
            result.Messages.Add("Brak służb z godziną rozpoczęcia i zakończenia.");
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

        var existingBlockingAssignments = await _dbContext.PlanningAssignments
            .Include(x => x.PlanningDuty)
            .Where(x => x.CompanyId == companyId
                && x.Date >= request.DateFrom.AddDays(-1)
                && x.Date <= request.DateTo
                && x.Status != PlanningAssignmentStatus.Generated)
            .ToListAsync(cancellationToken);

        var unavailableDriverDays = await GetUnavailableDriverDaysAsync(
            companyId,
            drivers.Select(x => x.Id).ToHashSet(),
            request.DateFrom,
            request.DateTo,
            cancellationToken);

        result.ManualAssignmentsPreserved = existingBlockingAssignments.Count(x => x.Status == PlanningAssignmentStatus.Manual);

        var plannedIntervalsByDriver = existingBlockingAssignments
            .Where(x => x.StartDateTime.HasValue && x.EndDateTime.HasValue)
            .GroupBy(x => x.DriverId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => new AssignmentInterval(y.StartDateTime!.Value, y.EndDateTime!.Value)).ToList());

        var occupiedDriverDays = existingBlockingAssignments
            .Where(x => x.Date >= request.DateFrom && x.Date <= request.DateTo)
            .Select(x => (x.DriverId, x.Date))
            .ToHashSet();
        foreach (var unavailableDay in unavailableDriverDays)
        {
            occupiedDriverDays.Add(unavailableDay);
        }

        var nextDriverIndex = 0;
        foreach (var date in EachDate(request.DateFrom, request.DateTo))
        {
            var schedule = schedules[(date.Year, date.Month)];
            foreach (var duty in duties)
            {
                var interval = BuildInterval(date, duty);
                if (interval is null)
                {
                    continue;
                }

                var driver = FindAvailableDriver(
                    drivers,
                    plannedIntervalsByDriver,
                    occupiedDriverDays,
                    interval.Value,
                    date,
                    ref nextDriverIndex);

                if (driver is null)
                {
                    result.ConflictCount++;
                    result.Messages.Add($"Nie znaleziono wolnego kierowcy dla służby {duty.DutyNumber} w dniu {date:yyyy-MM-dd}.");
                    continue;
                }

                var assignment = new PlanningAssignment
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
                    Notes = "Wygenerowano automatycznie.",
                    CreatedAt = now,
                    CreatedUtc = now
                };

                _dbContext.PlanningAssignments.Add(assignment);
                if (!plannedIntervalsByDriver.TryGetValue(driver.Id, out var intervals))
                {
                    intervals = new List<AssignmentInterval>();
                    plannedIntervalsByDriver[driver.Id] = intervals;
                }
                intervals.Add(interval.Value);
                occupiedDriverDays.Add((driver.Id, date));
                result.GeneratedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        result.Messages.Add($"Wygenerowano {result.GeneratedCount} przypisań automatycznych.");

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

    internal static AssignmentInterval? BuildInterval(DateOnly workDate, PlanningDuty duty)
    {
        if (!duty.StartTime.HasValue || !duty.EndTime.HasValue)
        {
            return null;
        }

        var start = workDate.ToDateTime(duty.StartTime.Value);
        var end = workDate.ToDateTime(duty.EndTime.Value);
        if (duty.EndTime.Value < duty.StartTime.Value)
        {
            end = end.AddDays(1);
        }

        return new AssignmentInterval(start, end);
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


    private async Task<HashSet<(Guid DriverId, DateOnly Date)>> GetUnavailableDriverDaysAsync(
        Guid companyId,
        ISet<Guid> driverIds,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<(Guid DriverId, DateOnly Date)>();
        if (driverIds.Count == 0)
        {
            return result;
        }

        var availabilities = await _dbContext.PlanningDriverAvailabilities
            .Where(x => x.CompanyId == companyId
                && driverIds.Contains(x.DriverId)
                && x.DateFrom <= dateTo
                && x.DateTo >= dateFrom)
            .ToListAsync(cancellationToken);

        foreach (var availability in availabilities)
        {
            var start = availability.DateFrom < dateFrom ? dateFrom : availability.DateFrom;
            var end = availability.DateTo > dateTo ? dateTo : availability.DateTo;
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                result.Add((availability.DriverId, date));
            }
        }

        return result;
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

    private static void ValidateRequest(PlanningAutoGenerateRequestDto request)
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

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }

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

    private static string FormatDriverName(Driver driver)
    {
        var name = $"{driver.FirstName} {driver.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? driver.CardNumber : name;
    }
}

public readonly record struct AssignmentInterval(DateTime Start, DateTime End);

