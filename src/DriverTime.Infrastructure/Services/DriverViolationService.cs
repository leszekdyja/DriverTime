using System.Globalization;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverViolationService : IDriverViolationService
{
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromHours(4.5);
    private static readonly TimeSpan FullBreak = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan FirstSplitBreak = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SecondSplitBreak = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DailyDrivingLimit = TimeSpan.FromHours(9);
    private static readonly TimeSpan ExtendedDailyDrivingLimit = TimeSpan.FromHours(10);
    private static readonly TimeSpan ReducedDailyRest = TimeSpan.FromHours(9);
    private static readonly TimeSpan RegularDailyRest = TimeSpan.FromHours(11);
    private static readonly TimeSpan WeeklyDrivingLimit = TimeSpan.FromHours(56);
    private static readonly TimeSpan TwoWeekDrivingLimit = TimeSpan.FromHours(90);
    private static readonly TimeSpan ReducedWeeklyRest = TimeSpan.FromHours(24);
    private static readonly TimeSpan RegularWeeklyRest = TimeSpan.FromHours(45);
    private static readonly TimeSpan RestMergeTolerance = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FerryInterruptionLimit = TimeSpan.FromHours(1);

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverViolationService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DriverViolationDto>> GetViolationsAsync(
        CancellationToken cancellationToken = default)
    {
        var activities = await GetActivitiesQuery()
            .OrderBy(x => x.StartUtc)
            .ToListAsync(cancellationToken);

        return CalculateViolations(activities);
    }

    public async Task<IReadOnlyList<DriverViolationDto>?> GetViolationsForDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId,
                cancellationToken);

        if (!driverExists)
        {
            return null;
        }

        var activities = await GetActivitiesQuery()
            .Where(x => x.DddFile.DriverId == driverId)
            .OrderBy(x => x.StartUtc)
            .ToListAsync(cancellationToken);

        return CalculateViolations(activities);
    }

    private IQueryable<DriverActivity> GetActivitiesQuery()
    {
        return _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x => x.DddFile.CompanyId == _currentUser.CompanyId)
            .Include(x => x.DddFile);
    }

    private static IReadOnlyList<DriverViolationDto> CalculateViolations(
        IReadOnlyList<DriverActivity> activities)
    {
        var violations = new List<DriverViolationDto>();

        foreach (var driverActivities in activities.GroupBy(GetDriverKey))
        {
            var ordered = driverActivities.OrderBy(x => x.StartUtc).ToList();

            if (ordered.Count == 0)
            {
                continue;
            }

            var restBlocks = BuildRestBlocks(ordered);

            AddContinuousDrivingViolations(ordered, violations);
            AddDailyDrivingViolations(ordered, violations);
            AddDailyRestViolations(ordered, restBlocks, violations);
            AddWeeklyDrivingViolations(ordered, violations);
            AddWeeklyRestViolations(ordered, restBlocks, violations);
            AddWeeklyRestCompensationViolations(ordered, restBlocks, violations);
            AddFerryTrainViolations(ordered, violations);
        }

        return violations
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenBy(x => x.Code)
            .ToList();
    }

    private static void AddContinuousDrivingViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var driving = TimeSpan.Zero;
        var firstSplitTaken = false;
        DateTime? periodStart = null;
        DriverActivity? lastDriving = null;

        foreach (var activity in activities)
        {
            var duration = GetDuration(activity);

            if (IsActivity(activity, "DRIVING"))
            {
                periodStart ??= activity.StartUtc;
                driving += duration;
                lastDriving = activity;
                continue;
            }

            if (!IsBreakActivity(activity))
            {
                continue;
            }

            var completesBreak = duration >= FullBreak
                || (firstSplitTaken && duration >= SecondSplitBreak);

            if (completesBreak)
            {
                AddContinuousDrivingViolation(
                    lastDriving,
                    periodStart,
                    driving,
                    violations);

                driving = TimeSpan.Zero;
                periodStart = null;
                lastDriving = null;
                firstSplitTaken = false;
            }
            else if (!firstSplitTaken && duration >= FirstSplitBreak)
            {
                firstSplitTaken = true;
            }
        }

        AddContinuousDrivingViolation(lastDriving, periodStart, driving, violations);
    }

    private static void AddContinuousDrivingViolation(
        DriverActivity? activity,
        DateTime? periodStart,
        TimeSpan driving,
        ICollection<DriverViolationDto> violations)
    {
        if (activity is null || driving <= ContinuousDrivingLimit)
        {
            return;
        }

        violations.Add(CreateViolation(
            activity,
            "CONTINUOUS_DRIVING_OVER_4H30",
            "Brak prawidlowej pauzy po 4h30 jazdy",
            periodStart ?? activity.StartUtc,
            activity.EndUtc,
            driving,
            ContinuousDrivingLimit,
            $"Jazda trwala {FormatDuration(driving)} bez pauzy 45 minut lub prawidlowej sekwencji 15 + 30 minut.",
            "high"));
    }

    private static void AddDailyDrivingViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        foreach (var week in activities
                     .Where(x => IsActivity(x, "DRIVING"))
                     .GroupBy(x => GetWeekStart(x.StartUtc)))
        {
            var extendedDaysUsed = 0;

            foreach (var day in week.GroupBy(x => x.StartUtc.Date).OrderBy(x => x.Key))
            {
                var driving = SumDuration(day);
                var firstActivity = day.OrderBy(x => x.StartUtc).First();

                if (driving > DailyDrivingLimit)
                {
                    extendedDaysUsed++;
                }

                if (driving > ExtendedDailyDrivingLimit)
                {
                    violations.Add(CreateViolation(
                        firstActivity,
                        "DAILY_DRIVING_OVER_10H",
                        "Przekroczony dzienny czas jazdy 10h",
                        day.Key,
                        day.Max(x => x.EndUtc),
                        driving,
                        ExtendedDailyDrivingLimit,
                        $"Dzienny czas jazdy wyniosl {FormatDuration(driving)} i przekroczyl bezwzgledny limit 10 godzin.",
                        "high"));
                    continue;
                }

                if (driving <= DailyDrivingLimit)
                {
                    continue;
                }

                if (extendedDaysUsed <= 2)
                {
                    continue;
                }

                violations.Add(CreateViolation(
                    firstActivity,
                    "DAILY_DRIVING_EXTENSION_OVER_TWO_PER_WEEK",
                    "Trzecie wydluzenie jazdy do 10h w tygodniu",
                    day.Key,
                    day.Max(x => x.EndUtc),
                    driving,
                    DailyDrivingLimit,
                    $"To {extendedDaysUsed}. dzien w tygodniu z jazda powyzej 9 godzin. Dozwolone sa najwyzej dwa takie wydluzenia.",
                    "medium"));
            }
        }
    }

    private static void AddDailyRestViolations(
        IReadOnlyList<DriverActivity> activities,
        IReadOnlyList<RestBlock> restBlocks,
        ICollection<DriverViolationDto> violations)
    {
        var qualifyingRests = restBlocks
            .Where(x => x.RestDuration >= ReducedDailyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();

        if (qualifyingRests.Count == 0)
        {
            AddNoDailyRestViolation(activities, violations);
            return;
        }

        var reducedRestsSinceWeeklyRest = 0;

        for (var index = 0; index < qualifyingRests.Count; index++)
        {
            var current = qualifyingRests[index];

            if (current.RestDuration >= ReducedWeeklyRest)
            {
                reducedRestsSinceWeeklyRest = 0;
            }
            else if (current.RestDuration < RegularDailyRest)
            {
                var previousQualifyingEnd = index > 0
                    ? qualifyingRests[index - 1].EndUtc
                    : activities[0].StartUtc;
                var isSplitRegularRest = restBlocks.Any(x =>
                    x.StartUtc >= previousQualifyingEnd
                    && x.EndUtc <= current.StartUtc
                    && x.RestDuration >= TimeSpan.FromHours(3)
                    && x.RestDuration < ReducedDailyRest
                    && x.RestDuration + current.RestDuration >= TimeSpan.FromHours(12));

                if (!isSplitRegularRest)
                {
                    reducedRestsSinceWeeklyRest++;

                    if (reducedRestsSinceWeeklyRest > 3)
                    {
                        violations.Add(CreateViolation(
                            current.ReferenceActivity,
                            "DAILY_REST_REDUCTIONS_OVER_THREE",
                            "Zbyt wiele skroconych odpoczynkow dziennych",
                            current.StartUtc,
                            current.EndUtc,
                            current.RestDuration,
                            RegularDailyRest,
                            "Wykryto wiecej niz trzy odpoczynki dzienne trwajace od 9 do 11 godzin pomiedzy odpoczynkami tygodniowymi.",
                            "medium"));
                    }
                }
            }

            var anchor = current.EndUtc;
            var next = qualifyingRests.Skip(index + 1).FirstOrDefault();
            var multiManning = HasMarkerBetween(activities, anchor, next?.EndUtc, IsMultiManningMarker);
            var window = multiManning ? TimeSpan.FromHours(30) : TimeSpan.FromHours(24);
            var deadline = anchor + window;

            if (next is not null && next.EndUtc <= deadline)
            {
                continue;
            }

            var dataEnd = activities[^1].EndUtc;

            if (dataEnd <= deadline)
            {
                continue;
            }

            var actualEnd = next?.EndUtc ?? dataEnd;
            violations.Add(CreateViolation(
                current.ReferenceActivity,
                multiManning
                    ? "MULTI_MANNING_DAILY_REST_OVER_30H"
                    : "DAILY_REST_NOT_COMPLETED_IN_24H",
                multiManning
                    ? "Brak odpoczynku 9h w okresie 30h przy zalodze"
                    : "Odpoczynek dzienny nieodbyty w okresie 24h",
                anchor,
                actualEnd,
                actualEnd - anchor,
                window,
                multiManning
                    ? "Przy zalodze kilkuosobowej odpoczynek co najmniej 9 godzin nie zostal zakonczony w okresie 30 godzin."
                    : "Odpoczynek co najmniej 9 godzin nie zostal zakonczony w ciagu 24 godzin od konca poprzedniego odpoczynku.",
                "high"));
        }
    }

    private static void AddNoDailyRestViolation(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var span = activities[^1].EndUtc - activities[0].StartUtc;

        if (span < TimeSpan.FromHours(24))
        {
            return;
        }

        violations.Add(CreateViolation(
            activities[0],
            "DAILY_REST_BELOW_9H",
            "Brak minimalnego odpoczynku dziennego 9h",
            activities[0].StartUtc,
            activities[^1].EndUtc,
            TimeSpan.Zero,
            ReducedDailyRest,
            "W analizowanym okresie nie znaleziono ciaglego odpoczynku dziennego trwajacego co najmniej 9 godzin.",
            "high"));
    }

    private static void AddWeeklyDrivingViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        var weeks = activities
            .Where(x => IsActivity(x, "DRIVING"))
            .GroupBy(x => GetWeekStart(x.StartUtc))
            .Select(group => new WeekDrivingSummary(
                group.Key,
                group.OrderBy(x => x.StartUtc).First(),
                SumDuration(group)))
            .OrderBy(x => x.WeekStartUtc)
            .ToList();

        foreach (var week in weeks.Where(x => x.DrivingDuration > WeeklyDrivingLimit))
        {
            violations.Add(CreateViolation(
                week.FirstActivity,
                "WEEKLY_DRIVING_OVER_56H",
                "Przekroczony tygodniowy czas jazdy 56h",
                week.WeekStartUtc,
                week.WeekStartUtc.AddDays(7),
                week.DrivingDuration,
                WeeklyDrivingLimit,
                $"Tygodniowy czas jazdy wyniosl {FormatDuration(week.DrivingDuration)}.",
                "high"));
        }

        for (var index = 1; index < weeks.Count; index++)
        {
            var previous = weeks[index - 1];
            var current = weeks[index];

            if (current.WeekStartUtc != previous.WeekStartUtc.AddDays(7))
            {
                continue;
            }

            var driving = previous.DrivingDuration + current.DrivingDuration;

            if (driving <= TwoWeekDrivingLimit)
            {
                continue;
            }

            violations.Add(CreateViolation(
                previous.FirstActivity,
                "TWO_WEEK_DRIVING_OVER_90H",
                "Przekroczony czas jazdy 90h w dwoch tygodniach",
                previous.WeekStartUtc,
                current.WeekStartUtc.AddDays(7),
                driving,
                TwoWeekDrivingLimit,
                $"Laczny czas jazdy w dwoch kolejnych tygodniach wyniosl {FormatDuration(driving)}.",
                "high"));
        }
    }

    private static void AddWeeklyRestViolations(
        IReadOnlyList<DriverActivity> activities,
        IReadOnlyList<RestBlock> restBlocks,
        ICollection<DriverViolationDto> violations)
    {
        var weeklyRests = restBlocks
            .Where(x => x.RestDuration >= ReducedWeeklyRest)
            .OrderBy(x => x.StartUtc)
            .ToList();

        for (var index = 1; index < weeklyRests.Count; index++)
        {
            var previous = weeklyRests[index - 1];
            var current = weeklyRests[index];
            var latestStart = previous.EndUtc.AddDays(6);

            if (current.StartUtc <= latestStart)
            {
                continue;
            }

            violations.Add(CreateViolation(
                current.ReferenceActivity,
                "WEEKLY_REST_STARTED_LATE",
                "Odpoczynek tygodniowy rozpoczal sie za pozno",
                previous.EndUtc,
                current.StartUtc,
                current.StartUtc - previous.EndUtc,
                TimeSpan.FromDays(6),
                $"Kolejny odpoczynek tygodniowy rozpoczal sie {FormatDuration(current.StartUtc - latestStart)} po terminie szesciu okresow 24-godzinnych.",
                "high"));
        }

        var firstWeek = GetWeekStart(activities[0].StartUtc);

        if (activities[0].StartUtc > firstWeek)
        {
            firstWeek = firstWeek.AddDays(7);
        }

        var dataEnd = activities[^1].EndUtc;

        for (var week = firstWeek; week.AddDays(14) <= dataEnd; week = week.AddDays(7))
        {
            var periodEnd = week.AddDays(14);
            var rests = weeklyRests
                .Where(x => x.StartUtc < periodEnd && x.EndUtc > week)
                .ToList();

            if (rests.Count >= 2 && rests.Any(x => x.RestDuration >= RegularWeeklyRest))
            {
                continue;
            }

            var reference = activities.First(x => x.EndUtc > week);
            violations.Add(CreateViolation(
                reference,
                "WEEKLY_REST_PATTERN_INVALID",
                "Nieprawidlowy uklad odpoczynkow tygodniowych",
                week,
                periodEnd,
                TimeSpan.FromHours(rests.Sum(x => x.RestDuration.TotalHours)),
                TimeSpan.FromHours(69),
                "W dwoch kolejnych tygodniach wymagane sa dwa odpoczynki tygodniowe, w tym co najmniej jeden regularny 45h i jeden regularny lub skrocony minimum 24h.",
                "high"));
        }
    }

    private static void AddWeeklyRestCompensationViolations(
        IReadOnlyList<DriverActivity> activities,
        IReadOnlyList<RestBlock> restBlocks,
        ICollection<DriverViolationDto> violations)
    {
        var debts = restBlocks
            .Where(x => x.RestDuration >= ReducedWeeklyRest && x.RestDuration < RegularWeeklyRest)
            .Select(x => new CompensationDebt(
                x,
                RegularWeeklyRest - x.RestDuration,
                GetWeekStart(x.StartUtc).AddDays(28)))
            .OrderBy(x => x.DeadlineUtc)
            .ToList();
        var candidates = restBlocks
            .Where(x => x.RestDuration >= ReducedDailyRest)
            .OrderBy(x => x.StartUtc)
            .Select(x => new CompensationCandidate(
                x,
                x.RestDuration - ReducedDailyRest))
            .ToList();
        var dataEnd = activities[^1].EndUtc;

        foreach (var debt in debts)
        {
            var remaining = debt.Duration;

            foreach (var candidate in candidates.Where(x =>
                         x.Block.StartUtc >= debt.Rest.EndUtc
                         && x.Block.EndUtc <= debt.DeadlineUtc
                         && x.AvailableExtra > TimeSpan.Zero))
            {
                var used = candidate.AvailableExtra < remaining
                    ? candidate.AvailableExtra
                    : remaining;
                candidate.AvailableExtra -= used;
                remaining -= used;

                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }
            }

            if (remaining <= TimeSpan.Zero || dataEnd < debt.DeadlineUtc)
            {
                continue;
            }

            violations.Add(CreateViolation(
                debt.Rest.ReferenceActivity,
                "WEEKLY_REST_COMPENSATION_MISSING",
                "Brak rekompensaty skroconego odpoczynku tygodniowego",
                debt.Rest.EndUtc,
                debt.DeadlineUtc,
                debt.Duration - remaining,
                debt.Duration,
                $"Do konca trzeciego tygodnia po skroceniu nie odebrano brakujacej rekompensaty. Pozostalo {FormatDuration(remaining)}.",
                "high"));
        }
    }

    private static void AddFerryTrainViolations(
        IReadOnlyList<DriverActivity> activities,
        ICollection<DriverViolationDto> violations)
    {
        for (var index = 0; index < activities.Count; index++)
        {
            if (!IsFerryTrainMarker(activities[index]))
            {
                continue;
            }

            var markers = new List<DriverActivity>();
            var cursor = index;

            while (cursor < activities.Count && IsFerryTrainMarker(activities[cursor]))
            {
                markers.Add(activities[cursor]);
                cursor++;
            }

            var total = SumDuration(markers);

            if (markers.Count <= 2 && total <= FerryInterruptionLimit)
            {
                index = cursor - 1;
                continue;
            }

            violations.Add(CreateViolation(
                markers[0],
                "FERRY_TRAIN_INTERRUPTION_EXCEEDED",
                "Przekroczone przerwanie odpoczynku na promie lub w pociagu",
                markers[0].StartUtc,
                markers[^1].EndUtc,
                total,
                FerryInterruptionLimit,
                "Regularny odpoczynek oznaczony jako prom/pociag zostal przerwany wiecej niz dwa razy lub lacznie na ponad jedna godzine.",
                "medium"));
            index = cursor - 1;
        }
    }

    private static List<RestBlock> BuildRestBlocks(
        IReadOnlyList<DriverActivity> activities)
    {
        var blocks = new List<RestBlock>();
        RestBlock? current = null;

        foreach (var activity in activities)
        {
            if (IsActivity(activity, "REST"))
            {
                if (current is not null && activity.StartUtc <= current.EndUtc + RestMergeTolerance)
                {
                    current.EndUtc = activity.EndUtc > current.EndUtc
                        ? activity.EndUtc
                        : current.EndUtc;
                    current.RestDuration += GetDuration(activity);
                }
                else
                {
                    if (current is not null)
                    {
                        blocks.Add(current);
                    }

                    current = new RestBlock(
                        activity.StartUtc,
                        activity.EndUtc,
                        GetDuration(activity),
                        activity);
                }

                continue;
            }

            if (current is not null
                && IsFerryTrainMarker(activity)
                && current.InterruptionCount < 2
                && current.InterruptionDuration + GetDuration(activity) <= FerryInterruptionLimit)
            {
                current.EndUtc = activity.EndUtc;
                current.InterruptionCount++;
                current.InterruptionDuration += GetDuration(activity);
                continue;
            }

            if (current is not null)
            {
                blocks.Add(current);
                current = null;
            }
        }

        if (current is not null)
        {
            blocks.Add(current);
        }

        return blocks;
    }

    private static DriverViolationDto CreateViolation(
        DriverActivity activity,
        string code,
        string violationType,
        DateTime occurredAtUtc,
        DateTime periodEndUtc,
        TimeSpan actualDuration,
        TimeSpan limitDuration,
        string description,
        string severity)
    {
        return new DriverViolationDto
        {
            Code = code,
            DriverFirstName = activity.DddFile.DriverFirstName,
            DriverLastName = activity.DddFile.DriverLastName,
            DriverCardNumber = activity.DddFile.DriverCardNumber,
            ViolationType = violationType,
            OccurredAtUtc = occurredAtUtc,
            PeriodEndUtc = periodEndUtc,
            Description = description,
            Severity = severity,
            ActualDurationMinutes = Math.Max((long)actualDuration.TotalMinutes, 0),
            LimitDurationMinutes = Math.Max((long)limitDuration.TotalMinutes, 0)
        };
    }

    private static string GetDriverKey(DriverActivity activity) =>
        activity.DddFile.DriverId?.ToString()
        ?? (string.IsNullOrWhiteSpace(activity.DddFile.DriverCardNumber)
            ? activity.DddFileId.ToString()
            : activity.DddFile.DriverCardNumber);

    private static DateTime GetWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);
        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }

    private static bool HasMarkerBetween(
        IEnumerable<DriverActivity> activities,
        DateTime start,
        DateTime? end,
        Func<DriverActivity, bool> predicate) =>
        activities.Any(x => x.StartUtc >= start && (!end.HasValue || x.StartUtc < end) && predicate(x));

    private static bool IsBreakActivity(DriverActivity activity) =>
        IsActivity(activity, "REST")
        || (IsMultiManningMarker(activity) && GetDuration(activity) >= FullBreak);

    private static bool IsMultiManningMarker(DriverActivity activity) =>
        IsAnyActivity(activity, "MULTI_MANNING", "MULTI_MANNING_AVAILABILITY", "CREW", "TEAM");

    private static bool IsFerryTrainMarker(DriverActivity activity) =>
        IsAnyActivity(activity, "FERRY", "TRAIN", "FERRY_TRAIN", "FERRY/TRAIN");

    private static bool IsAnyActivity(DriverActivity activity, params string[] values) =>
        values.Any(value => IsActivity(activity, value));

    private static bool IsActivity(DriverActivity activity, string value) =>
        activity.ActivityType.Equals(value, StringComparison.OrdinalIgnoreCase);

    private static TimeSpan GetDuration(DriverActivity activity)
    {
        var duration = activity.EndUtc - activity.StartUtc;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    private static TimeSpan SumDuration(IEnumerable<DriverActivity> activities) =>
        activities.Aggregate(TimeSpan.Zero, (total, activity) => total + GetDuration(activity));

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private sealed class RestBlock
    {
        public RestBlock(
            DateTime startUtc,
            DateTime endUtc,
            TimeSpan restDuration,
            DriverActivity referenceActivity)
        {
            StartUtc = startUtc;
            EndUtc = endUtc;
            RestDuration = restDuration;
            ReferenceActivity = referenceActivity;
        }

        public DateTime StartUtc { get; }
        public DateTime EndUtc { get; set; }
        public TimeSpan RestDuration { get; set; }
        public int InterruptionCount { get; set; }
        public TimeSpan InterruptionDuration { get; set; }
        public DriverActivity ReferenceActivity { get; }
    }

    private sealed record WeekDrivingSummary(
        DateTime WeekStartUtc,
        DriverActivity FirstActivity,
        TimeSpan DrivingDuration);

    private sealed record CompensationDebt(
        RestBlock Rest,
        TimeSpan Duration,
        DateTime DeadlineUtc);

    private sealed class CompensationCandidate
    {
        public CompensationCandidate(RestBlock block, TimeSpan availableExtra)
        {
            Block = block;
            AvailableExtra = availableExtra;
        }

        public RestBlock Block { get; }
        public TimeSpan AvailableExtra { get; set; }
    }
}
