using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Services;

public class ViolationDetectionService : IViolationDetectionService
{
    private static readonly TimeSpan DailyDrivingLimit = TimeSpan.FromHours(9);
    private static readonly TimeSpan AbsoluteDailyDrivingLimit = TimeSpan.FromHours(10);
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromHours(4.5);
    private static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan FirstSplitBreak = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SecondSplitBreak = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RequiredDailyRest = TimeSpan.FromHours(11);

    private static readonly string[] AutomaticViolationReferences =
    {
        "EU561:DAILY_DRIVING_OVER_9H",
        "EU561:DAILY_DRIVING_OVER_10H",
        "EU561:CONTINUOUS_DRIVING_WITHOUT_45M_BREAK",
        "EU561:DAILY_REST_BELOW_11H"
    };

    private readonly DriverTimeDbContext _dbContext;
    private readonly ILogger<ViolationDetectionService> _logger;

    public ViolationDetectionService(
        DriverTimeDbContext dbContext,
        ILogger<ViolationDetectionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> DetectForDriverAsync(
        Guid companyId,
        Guid driverId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var fromUtc = EnsureUtc(fromDate);
        var toUtc = EnsureUtc(toDate);

        if (toUtc < fromUtc)
        {
            return 0;
        }

        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == driverId && x.CompanyId == companyId,
                cancellationToken);

        if (!driverExists)
        {
            return 0;
        }

        var activities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Include(x => x.DddFile)
            .Where(x =>
                x.DddFile.CompanyId == companyId &&
                x.DddFile.DriverId == driverId &&
                x.StartUtc < toUtc &&
                x.EndUtc > fromUtc)
            .OrderBy(x => x.StartUtc)
            .ToListAsync(cancellationToken);

        LogDetectionInput(companyId, driverId, fromUtc, toUtc, activities);

        var violations = Detect(driverId, activities);

        var deletedCount = await _dbContext.Violations
            .Where(x =>
                x.DriverId == driverId &&
                x.Driver != null &&
                x.Driver.CompanyId == companyId &&
                AutomaticViolationReferences.Contains(x.RegulationReference) &&
                x.ViolationStart < toUtc &&
                x.ViolationEnd > fromUtc)
            .ExecuteDeleteAsync(cancellationToken);

        if (violations.Count == 0)
        {
            _logger.LogInformation(
                "Violation detection completed with no results for driver {DriverId}. CompanyId={CompanyId}, DeletedExisting={DeletedCount}.",
                driverId,
                companyId,
                deletedCount);

            return 0;
        }

        _dbContext.Violations.AddRange(violations);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Violation detection saved {SavedCount} violations for driver {DriverId}. CompanyId={CompanyId}, DeletedExisting={DeletedCount}, Range={FromUtc:o}..{ToUtc:o}.",
            violations.Count,
            driverId,
            companyId,
            deletedCount,
            fromUtc,
            toUtc);

        return violations.Count;
    }

    public async Task<int> DetectAfterImportAsync(
        Guid importId,
        CancellationToken cancellationToken = default)
    {
        var import = await _dbContext.DddFiles
            .AsNoTracking()
            .Include(x => x.DriverActivities)
            .FirstOrDefaultAsync(x => x.Id == importId, cancellationToken);

        if (import?.DriverId is not Guid driverId ||
            import.DriverActivities.Count == 0)
        {
            return 0;
        }

        var from = import.DriverActivities.Min(x => x.StartUtc).Date;
        var to = import.DriverActivities.Max(x => x.EndUtc).Date.AddDays(1);

        return await DetectForDriverAsync(
            import.CompanyId,
            driverId,
            from,
            to,
            cancellationToken);
    }

    private void LogDetectionInput(
        Guid companyId,
        Guid driverId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyCollection<DriverActivity> activities)
    {
        var firstStart = activities.Count == 0
            ? (DateTime?)null
            : activities.Min(x => x.StartUtc);
        var lastEnd = activities.Count == 0
            ? (DateTime?)null
            : activities.Max(x => x.EndUtc);
        var typeCounts = activities
            .GroupBy(x => x.ActivityType)
            .OrderByDescending(x => x.Count())
            .Take(10)
            .Select(x => $"{x.Key}:{x.Count()}");

        _logger.LogInformation(
            "Violation detection input for driver {DriverId}. CompanyId={CompanyId}, QueryRange={FromUtc:o}..{ToUtc:o}, Activities={ActivityCount}, FirstStart={FirstStart:o}, LastEnd={LastEnd:o}, Types={ActivityTypes}.",
            driverId,
            companyId,
            fromUtc,
            toUtc,
            activities.Count,
            firstStart,
            lastEnd,
            string.Join(", ", typeCounts));
    }

    private static List<Violation> Detect(
        Guid driverId,
        IReadOnlyList<DriverActivity> activities)
    {
        var ordered = activities
            .Where(x => x.EndUtc > x.StartUtc)
            .OrderBy(x => x.StartUtc)
            .ToList();
        var violations = new List<Violation>();

        if (ordered.Count == 0)
        {
            return violations;
        }

        AddDailyDrivingViolations(driverId, ordered, violations);
        AddContinuousDrivingViolations(driverId, ordered, violations);
        AddDailyRestViolations(driverId, ordered, violations);

        return violations;
    }

    private static void AddDailyDrivingViolations(
        Guid driverId,
        IReadOnlyList<DriverActivity> activities,
        ICollection<Violation> violations)
    {
        foreach (var day in activities
                     .Where(IsDriving)
                     .GroupBy(x => x.StartUtc.Date)
                     .OrderBy(x => x.Key))
        {
            var driving = SumDuration(day);

            if (driving > AbsoluteDailyDrivingLimit)
            {
                violations.Add(CreateViolation(
                    driverId,
                    "Dzienna jazda powyżej 10 godzin",
                    "EU561:DAILY_DRIVING_OVER_10H",
                    "Critical",
                    driving,
                    day.Key,
                    day.Max(x => x.EndUtc)));
            }
            else if (driving > DailyDrivingLimit)
            {
                violations.Add(CreateViolation(
                    driverId,
                    "Dzienna jazda powyżej 9 godzin",
                    "EU561:DAILY_DRIVING_OVER_9H",
                    "Warning",
                    driving,
                    day.Key,
                    day.Max(x => x.EndUtc)));
            }
        }
    }

    private static void AddContinuousDrivingViolations(
        Guid driverId,
        IReadOnlyList<DriverActivity> activities,
        ICollection<Violation> violations)
    {
        var driving = TimeSpan.Zero;
        var firstSplitTaken = false;
        DateTime? periodStart = null;
        DateTime? lastDrivingEnd = null;

        foreach (var activity in activities)
        {
            if (IsDriving(activity))
            {
                periodStart ??= activity.StartUtc;
                driving += GetDuration(activity);
                lastDrivingEnd = activity.EndUtc;
                continue;
            }

            var duration = GetDuration(activity);

            if (IsBreak(activity))
            {
                if (duration >= RequiredBreak ||
                    (firstSplitTaken && duration >= SecondSplitBreak))
                {
                    AddContinuousDrivingViolation(
                        driverId,
                        periodStart,
                        lastDrivingEnd,
                        driving,
                        violations);

                    driving = TimeSpan.Zero;
                    periodStart = null;
                    lastDrivingEnd = null;
                    firstSplitTaken = false;
                }
                else if (!firstSplitTaken && duration >= FirstSplitBreak)
                {
                    firstSplitTaken = true;
                }

                continue;
            }

            if (IsWork(activity))
            {
                firstSplitTaken = false;
            }
        }

        AddContinuousDrivingViolation(
            driverId,
            periodStart,
            lastDrivingEnd,
            driving,
            violations);
    }

    private static void AddContinuousDrivingViolation(
        Guid driverId,
        DateTime? periodStart,
        DateTime? periodEnd,
        TimeSpan driving,
        ICollection<Violation> violations)
    {
        if (!periodStart.HasValue ||
            !periodEnd.HasValue ||
            driving <= ContinuousDrivingLimit)
        {
            return;
        }

        violations.Add(CreateViolation(
            driverId,
            "Brak przerwy 45 minut po 4h30 jazdy",
            "EU561:CONTINUOUS_DRIVING_WITHOUT_45M_BREAK",
            "Critical",
            driving,
            periodStart.Value,
            periodEnd.Value));
    }

    private static void AddDailyRestViolations(
        Guid driverId,
        IReadOnlyList<DriverActivity> activities,
        ICollection<Violation> violations)
    {
        foreach (var day in activities.GroupBy(x => x.StartUtc.Date).OrderBy(x => x.Key))
        {
            var rest = day
                .Where(IsRest)
                .Select(GetDuration)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Max();

            if (rest >= RequiredDailyRest)
            {
                continue;
            }

            violations.Add(CreateViolation(
                driverId,
                "Odpoczynek dzienny krótszy niż 11 godzin",
                "EU561:DAILY_REST_BELOW_11H",
                "Warning",
                rest,
                day.Key,
                day.Max(x => x.EndUtc)));
        }
    }

    private static Violation CreateViolation(
        Guid driverId,
        string type,
        string regulationReference,
        string severity,
        TimeSpan duration,
        DateTime start,
        DateTime end)
    {
        return new Violation
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            ViolationType = type,
            RegulationReference = regulationReference,
            Severity = severity,
            DurationMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes)),
            ViolationStart = EnsureUtc(start),
            ViolationEnd = EnsureUtc(end),
            CalculatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static bool IsDriving(DriverActivity activity) =>
        activity.ActivityType.Equals("DRIVING", StringComparison.OrdinalIgnoreCase)
        || activity.ActivityType.Equals("JAZDA", StringComparison.OrdinalIgnoreCase);

    private static bool IsRest(DriverActivity activity) =>
        activity.ActivityType.Equals("REST", StringComparison.OrdinalIgnoreCase)
        || activity.ActivityType.Equals("BREAK", StringComparison.OrdinalIgnoreCase)
        || activity.ActivityType.Equals("ODPOCZYNEK", StringComparison.OrdinalIgnoreCase);

    private static bool IsAvailability(DriverActivity activity) =>
        activity.ActivityType.Equals("AVAILABILITY", StringComparison.OrdinalIgnoreCase);

    private static bool IsBreak(DriverActivity activity) =>
        IsRest(activity) || IsAvailability(activity);

    private static bool IsWork(DriverActivity activity) =>
        activity.ActivityType.Equals("WORK", StringComparison.OrdinalIgnoreCase)
        || activity.ActivityType.Equals("PRACA", StringComparison.OrdinalIgnoreCase);

    private static TimeSpan GetDuration(DriverActivity activity)
    {
        var duration = activity.EndUtc - activity.StartUtc;

        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    private static TimeSpan SumDuration(IEnumerable<DriverActivity> activities) =>
        activities.Aggregate(TimeSpan.Zero, (sum, activity) => sum + GetDuration(activity));

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
