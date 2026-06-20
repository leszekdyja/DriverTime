using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class DailyRestViolationRule : IComplianceRule
{
    private const string RuleCode = "DAILY_REST";
    private const long RegularDailyRestMinutes = 660;
    private const long ReducedDailyRestMinutes = 540;
    private const long FirstSplitDailyRestMinutes = 180;
    private static readonly TimeSpan BoundaryRestTolerance = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RegularDailyRest = TimeSpan.FromMinutes(RegularDailyRestMinutes);
    private static readonly TimeSpan ReducedDailyRest = TimeSpan.FromMinutes(ReducedDailyRestMinutes);
    private static readonly TimeSpan FirstSplitDailyRest = TimeSpan.FromMinutes(FirstSplitDailyRestMinutes);

    public string Code => RuleCode;

    public string Name => "Daily rest";

    private readonly ILogger<DailyRestViolationRule> _logger;

    public DailyRestViolationRule(ILogger<DailyRestViolationRule> logger)
    {
        _logger = logger;
    }

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var ordered = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ToList();

        var dutyActivities = ordered
            .Where(IsDutyActivity)
            .ToList();

        if (dutyActivities.Count == 0)
        {
            _logger.LogInformation(
                "Compliance rule {RuleCode} driver {DriverId}: dutyPeriods=0, maxLongestRestMinutes=0, periodsBelow11h=0, periodsBelow9h=0, violations=0.",
                RuleCode,
                driverId);

            return result;
        }

        var restPeriods = BuildContinuousRestPeriods(ordered);
        var dutyPeriods = 0;
        var periodsBelowRegularRest = 0;
        var periodsBelowReducedRest = 0;
        var maxLongestRest = TimeSpan.Zero;
        var currentPeriodStart = dutyActivities[0].StartUtc;

        while (true)
        {
            dutyPeriods++;
            var windowEnd = currentPeriodStart.AddHours(24);
            var allowBoundaryRest = currentPeriodStart == dutyActivities[0].StartUtc;
            var longestRest = FindLongestRestInWindow(restPeriods, currentPeriodStart, windowEnd, allowBoundaryRest);
            var splitRegularRest = FindSplitRegularDailyRestInWindow(restPeriods, currentPeriodStart, windowEnd);
            var restMinutes = (long)Math.Round(longestRest.Duration.TotalMinutes);
            maxLongestRest = Max(maxLongestRest, longestRest.Duration);

            if (longestRest.Duration >= RegularDailyRest || splitRegularRest is not null)
            {
                var restEndUtc = splitRegularRest?.SecondRest.EndUtc ?? longestRest.EndUtc;
                var nextDuty = restEndUtc <= currentPeriodStart
                    ? GetNextDutyAfterPeriodStart(dutyActivities, currentPeriodStart)
                    : GetNextDutyAfterRest(dutyActivities, restEndUtc);
                if (nextDuty is null)
                {
                    break;
                }

                currentPeriodStart = nextDuty.StartUtc;
                continue;
            }

            periodsBelowRegularRest++;

            if (longestRest.Duration >= ReducedDailyRest)
            {
                result.Violations.Add(CreateViolation(
                    severity: "MEDIUM",
                    description: $"Wykryto skrócony odpoczynek dzienny: {FormatDuration(longestRest.Duration)} zamiast regularnych 11 godzin.",
                    startUtc: currentPeriodStart,
                    endUtc: longestRest.EndUtc,
                    analysisWindowEndUtc: windowEnd,
                    longestRest: longestRest,
                    reason: "ReducedDailyRest"));

                var nextDuty = longestRest.EndUtc <= currentPeriodStart
                    ? GetNextDutyAfterPeriodStart(dutyActivities, currentPeriodStart)
                    : GetNextDutyAfterRest(dutyActivities, longestRest.EndUtc);
                if (nextDuty is null)
                {
                    break;
                }

                currentPeriodStart = nextDuty.StartUtc;
                continue;
            }

            if (!ShouldReportMissingReducedRest(dutyActivities, restPeriods, currentPeriodStart, windowEnd, longestRest))
            {
                break;
            }

            periodsBelowReducedRest++;

            result.Violations.Add(CreateViolation(
                severity: "HIGH",
                description: "Nie znaleziono ciągłego odpoczynku minimum 9 godzin w wymaganym oknie 24h.",
                startUtc: currentPeriodStart,
                endUtc: GetViolationEndUtc(dutyActivities, currentPeriodStart, windowEnd),
                analysisWindowEndUtc: windowEnd,
                longestRest: longestRest,
                reason: "MissingContinuousReducedDailyRest"));

            var nextPeriodStart = dutyActivities
                .FirstOrDefault(x => x.StartUtc >= windowEnd);

            if (nextPeriodStart is null)
            {
                break;
            }

            currentPeriodStart = nextPeriodStart.StartUtc;
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: dutyPeriods={DutyPeriods}, maxLongestRestMinutes={MaxLongestRestMinutes}, periodsBelow11h={PeriodsBelowRegularRest}, periodsBelow9h={PeriodsBelowReducedRest}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            dutyPeriods,
            (long)Math.Round(maxLongestRest.TotalMinutes),
            periodsBelowRegularRest,
            periodsBelowReducedRest,
            result.Violations.Count);

        return result;
    }

    private static ComplianceViolationCandidate CreateViolation(
        string severity,
        string description,
        DateTime startUtc,
        DateTime endUtc,
        DateTime analysisWindowEndUtc,
        RestPeriod longestRest,
        string reason)
    {
        var longestRestMinutes = (long)Math.Round(longestRest.Duration.TotalMinutes);
        var requiredRestMinutes = reason == "MissingContinuousReducedDailyRest"
            ? ReducedDailyRestMinutes
            : RegularDailyRestMinutes;
        var missingRestMinutes = Math.Max(requiredRestMinutes - longestRestMinutes, 0);
        var finalDescription = BuildDescription(reason, startUtc, analysisWindowEndUtc, longestRest.Duration);

        return new ComplianceViolationCandidate
        {
            Code = RuleCode,
            RuleName = "Daily rest",
            Severity = severity,
            Description = finalDescription,
            PeriodStartUtc = startUtc,
            PeriodEndUtc = endUtc,
            ActualMinutes = longestRestMinutes,
            LimitMinutes = RegularDailyRestMinutes,
            Metadata = new Dictionary<string, object>
            {
                ["restMinutes"] = longestRestMinutes,
                ["actualRestMinutes"] = longestRestMinutes,
                ["requiredRestMinutes"] = requiredRestMinutes,
                ["missingRestMinutes"] = missingRestMinutes,
                ["longestRestMinutes"] = longestRestMinutes,
                ["analysisWindowStartUtc"] = startUtc.ToString("O"),
                ["analysisWindowEndUtc"] = analysisWindowEndUtc.ToString("O"),
                ["longestRestStartUtc"] = longestRest.StartUtc.ToString("O"),
                ["longestRestEndUtc"] = longestRest.EndUtc.ToString("O"),
                ["requiredReducedRestMinutes"] = ReducedDailyRestMinutes,
                ["requiredRegularRestMinutes"] = RegularDailyRestMinutes,
                ["regularDailyRestMinutes"] = RegularDailyRestMinutes,
                ["reducedDailyRestMinutes"] = ReducedDailyRestMinutes,
                ["firstSplitDailyRestMinutes"] = FirstSplitDailyRestMinutes,
                ["reason"] = reason
            }
        };
    }

    private static IReadOnlyList<RestPeriod> BuildContinuousRestPeriods(IReadOnlyList<TimelineActivity> ordered)
    {
        var restPeriods = new List<RestPeriod>();

        for (var index = 0; index < ordered.Count; index++)
        {
            var activity = ordered[index];

            if (IsRestCompatible(activity))
            {
                restPeriods.Add(new RestPeriod(activity.StartUtc, activity.EndUtc));
            }

            if (index + 1 >= ordered.Count)
            {
                continue;
            }

            var next = ordered[index + 1];
            if (activity.EndUtc < next.StartUtc)
            {
                restPeriods.Add(new RestPeriod(activity.EndUtc, next.StartUtc));
            }
        }

        return MergeRestPeriods(restPeriods);
    }

    private static IReadOnlyList<RestPeriod> MergeRestPeriods(IEnumerable<RestPeriod> periods)
    {
        var ordered = periods
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var merged = new List<RestPeriod>();

        foreach (var period in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(period);
                continue;
            }

            var previous = merged[^1];
            if (period.StartUtc <= previous.EndUtc)
            {
                merged[^1] = previous with
                {
                    EndUtc = Max(previous.EndUtc, period.EndUtc)
                };
                continue;
            }

            merged.Add(period);
        }

        return merged;
    }

    private static RestPeriod FindLongestRestInWindow(
        IReadOnlyList<RestPeriod> restPeriods,
        DateTime windowStart,
        DateTime windowEnd,
        bool allowBoundaryRest)
    {
        var boundaryRest = allowBoundaryRest
            ? restPeriods
                .Where(x =>
                    x.StartUtc < windowStart &&
                    x.EndUtc >= windowStart.Subtract(BoundaryRestTolerance) &&
                    x.Duration >= ReducedDailyRest)
                .OrderByDescending(x => x.Duration)
                .ThenBy(x => x.StartUtc)
                .FirstOrDefault()
            : null;

        var longestRestInWindow = restPeriods
            .Where(x => x.StartUtc < windowEnd && x.EndUtc > windowStart)
            .Select(x => new RestPeriod(Max(x.StartUtc, windowStart), Min(x.EndUtc, windowEnd)))
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderByDescending(x => x.Duration)
            .ThenBy(x => x.StartUtc)
            .FirstOrDefault();

        if (boundaryRest is not null &&
            (longestRestInWindow is null || boundaryRest.Duration >= longestRestInWindow.Duration))
        {
            return boundaryRest;
        }

        return longestRestInWindow ?? new RestPeriod(windowStart, windowStart);
    }

    private static SplitRegularRest? FindSplitRegularDailyRestInWindow(
        IReadOnlyList<RestPeriod> restPeriods,
        DateTime windowStart,
        DateTime windowEnd)
    {
        var restsInWindow = restPeriods
            .Where(x => x.StartUtc < windowEnd && x.EndUtc > windowStart)
            .Select(x => new RestPeriod(Max(x.StartUtc, windowStart), Min(x.EndUtc, windowEnd)))
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        for (var firstIndex = 0; firstIndex < restsInWindow.Count; firstIndex++)
        {
            var firstRest = restsInWindow[firstIndex];
            if (firstRest.Duration < FirstSplitDailyRest)
            {
                continue;
            }

            for (var secondIndex = firstIndex + 1; secondIndex < restsInWindow.Count; secondIndex++)
            {
                var secondRest = restsInWindow[secondIndex];
                if (secondRest.Duration >= ReducedDailyRest)
                {
                    return new SplitRegularRest(firstRest, secondRest);
                }
            }
        }

        return null;
    }

    private static TimelineActivity? GetNextDutyAfterRest(
        IReadOnlyList<TimelineActivity> dutyActivities,
        DateTime restEndUtc)
    {
        return dutyActivities.FirstOrDefault(x => x.StartUtc >= restEndUtc);
    }

    private static TimelineActivity? GetNextDutyAfterPeriodStart(
        IReadOnlyList<TimelineActivity> dutyActivities,
        DateTime currentPeriodStart)
    {
        return dutyActivities.FirstOrDefault(x => x.StartUtc > currentPeriodStart);
    }

    private static bool ShouldReportMissingReducedRest(
        IReadOnlyList<TimelineActivity> dutyActivities,
        IReadOnlyList<RestPeriod> restPeriods,
        DateTime currentPeriodStart,
        DateTime windowEnd,
        RestPeriod longestRest)
    {
        if (dutyActivities.Any(x => x.StartUtc >= windowEnd))
        {
            return true;
        }

        if (longestRest.Duration > TimeSpan.Zero &&
            dutyActivities.Any(x => x.StartUtc >= longestRest.EndUtc && x.StartUtc < windowEnd))
        {
            return true;
        }

        return restPeriods.Any(x =>
            x.StartUtc >= currentPeriodStart &&
            x.EndUtc <= windowEnd &&
            x.Duration < ReducedDailyRest &&
            dutyActivities.Any(activity => activity.StartUtc >= x.EndUtc && activity.StartUtc < windowEnd));
    }

    private static DateTime GetViolationEndUtc(
        IReadOnlyList<TimelineActivity> dutyActivities,
        DateTime currentPeriodStart,
        DateTime windowEnd)
    {
        return dutyActivities
            .Where(x => x.StartUtc >= currentPeriodStart && x.StartUtc <= windowEnd)
            .Select(x => x.EndUtc)
            .DefaultIfEmpty(windowEnd)
            .Max();
    }

    private static bool IsRestCompatible(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase) ||
        activity.ActivityType.Equals(ActivityTypeNormalizer.Availability, StringComparison.OrdinalIgnoreCase);

    private static bool IsDutyActivity(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase) ||
        activity.ActivityType.Equals(ActivityTypeNormalizer.Work, StringComparison.OrdinalIgnoreCase);

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static string BuildDescription(
        string reason,
        DateTime analysisWindowStartUtc,
        DateTime analysisWindowEndUtc,
        TimeSpan longestRest)
    {
        var window = $"od {FormatDateTime(analysisWindowStartUtc)} do {FormatDateTime(analysisWindowEndUtc)}";

        return reason == "ReducedDailyRest"
            ? $"Wykryto skrócony odpoczynek dzienny w wymaganym oknie 24h {window}. Najdłuższy ciągły odpoczynek wyniósł {FormatDuration(longestRest)} zamiast regularnych 11 godzin."
            : $"Nie znaleziono ciągłego odpoczynku minimum 9 godzin w wymaganym oknie 24h {window}. Najdłuższy ciągły odpoczynek wyniósł {FormatDuration(longestRest)}.";
    }

    private static string FormatDateTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'");

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;

    private static DateTime Max(DateTime left, DateTime right) =>
        left >= right ? left : right;

    private static DateTime Min(DateTime left, DateTime right) =>
        left <= right ? left : right;

    private sealed record RestPeriod(DateTime StartUtc, DateTime EndUtc)
    {
        public TimeSpan Duration => EndUtc > StartUtc
            ? EndUtc - StartUtc
            : TimeSpan.Zero;
    }

    private sealed record SplitRegularRest(RestPeriod FirstRest, RestPeriod SecondRest);
}
