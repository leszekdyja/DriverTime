using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class DailyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "DAILY_DRIVING_LIMIT";
    private const int MaxWeeklyExtensions = 2;
    private static readonly TimeSpan StandardDailyLimit = TimeSpan.FromHours(9);
    private static readonly TimeSpan ExtendedDailyLimit = TimeSpan.FromHours(10);
    private static readonly TimeSpan DailyRestReset = TimeSpan.FromHours(9);

    public string Code => RuleCode;

    public string Name => "Daily driving limit";

    private readonly ILogger<DailyDrivingLimitRule> _logger;

    public DailyDrivingLimitRule(ILogger<DailyDrivingLimitRule> logger)
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

        var effectiveTimeline = WeeklyDrivingTimelineHelper.GetMergedDrivingTimeline(timeline)
            .Concat(timeline.Where(activity => !IsDriving(activity)))
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
        var drivingPeriods = BuildDriverDayDrivingPeriods(effectiveTimeline)
            .OrderBy(x => x.PeriodStartUtc)
            .ToList();
        var extendedDrivingDaysByWeek = new Dictionary<DateTime, int>();

        foreach (var period in drivingPeriods)
        {
            if (period.TotalDriving <= StandardDailyLimit)
            {
                continue;
            }

            var extensionNumber = GetNextExtensionNumber(
                extendedDrivingDaysByWeek,
                period.WeekStartUtc);

            if (period.TotalDriving <= ExtendedDailyLimit &&
                extensionNumber <= MaxWeeklyExtensions)
            {
                continue;
            }

            var exceededExtendedLimit = period.TotalDriving > ExtendedDailyLimit;
            var severity = exceededExtendedLimit ? "HIGH" : "MEDIUM";
            var limit = exceededExtendedLimit ? ExtendedDailyLimit : StandardDailyLimit;
            var exceededMinutes = Math.Max(
                0,
                (long)Math.Round((period.TotalDriving - limit).TotalMinutes));

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = severity,
                Description = BuildMessage(period.TotalDriving, exceededExtendedLimit),
                PeriodStartUtc = period.PeriodStartUtc,
                PeriodEndUtc = period.PeriodEndUtc,
                ActualMinutes = (long)Math.Round(period.TotalDriving.TotalMinutes),
                LimitMinutes = (long)limit.TotalMinutes,
                Metadata = new Dictionary<string, object>
                {
                    ["totalDrivingMinutes"] = (long)Math.Round(period.TotalDriving.TotalMinutes),
                    ["limitMinutes"] = (long)Math.Round(limit.TotalMinutes),
                    ["standardDailyLimitMinutes"] = (long)Math.Round(StandardDailyLimit.TotalMinutes),
                    ["extendedDailyLimitMinutes"] = (long)Math.Round(ExtendedDailyLimit.TotalMinutes),
                    ["dailyRestResetMinutes"] = (long)Math.Round(DailyRestReset.TotalMinutes),
                    ["exceededMinutes"] = exceededMinutes,
                    ["isoWeekStartUtc"] = period.WeekStartUtc.ToString("O"),
                    ["weeklyExtensionNumber"] = extensionNumber,
                    ["maxWeeklyExtensions"] = MaxWeeklyExtensions
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: drivingPeriods={DrivingPeriods}, maxDailyDrivingMinutes={MaxDailyDrivingMinutes}, periodsOver9h={PeriodsOverStandardLimit}, periodsOver10h={PeriodsOverExtendedLimit}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            drivingPeriods.Count,
            drivingPeriods.Count == 0 ? 0 : (long)Math.Round(drivingPeriods.Max(x => x.TotalDriving.TotalMinutes)),
            drivingPeriods.Count(x => x.TotalDriving > StandardDailyLimit),
            drivingPeriods.Count(x => x.TotalDriving > ExtendedDailyLimit),
            result.Violations.Count);

        return result;
    }

    private static IReadOnlyList<DailyDrivingSummary> BuildDriverDayDrivingPeriods(
        IReadOnlyList<TimelineActivity> timeline)
    {
        var ordered = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
        var periods = new List<DailyDrivingSummary>();
        DateTime? periodStartUtc = null;
        DateTime? periodEndUtc = null;
        DateTime? previousEndUtc = null;
        var totalDriving = TimeSpan.Zero;

        foreach (var activity in ordered)
        {
            if (periodStartUtc.HasValue &&
                previousEndUtc.HasValue &&
                activity.StartUtc - previousEndUtc.Value >= DailyRestReset)
            {
                AddPeriodIfDriving(periods, periodStartUtc, periodEndUtc, totalDriving);
                periodStartUtc = null;
                periodEndUtc = null;
                totalDriving = TimeSpan.Zero;
            }

            if (IsDailyRestReset(activity))
            {
                AddPeriodIfDriving(periods, periodStartUtc, periodEndUtc, totalDriving);
                periodStartUtc = null;
                periodEndUtc = null;
                totalDriving = TimeSpan.Zero;
                previousEndUtc = Max(previousEndUtc, activity.EndUtc);
                continue;
            }

            periodStartUtc ??= activity.StartUtc;
            periodEndUtc = Max(periodEndUtc, activity.EndUtc);
            previousEndUtc = Max(previousEndUtc, activity.EndUtc);

            if (IsDriving(activity))
            {
                totalDriving += activity.Duration;
            }
        }

        AddPeriodIfDriving(periods, periodStartUtc, periodEndUtc, totalDriving);

        return periods;
    }

    private static void AddPeriodIfDriving(
        ICollection<DailyDrivingSummary> periods,
        DateTime? periodStartUtc,
        DateTime? periodEndUtc,
        TimeSpan totalDriving)
    {
        if (!periodStartUtc.HasValue ||
            !periodEndUtc.HasValue ||
            totalDriving <= TimeSpan.Zero)
        {
            return;
        }

        periods.Add(new DailyDrivingSummary(
            periodStartUtc.Value,
            periodEndUtc.Value,
            GetIsoWeekStart(periodStartUtc.Value),
            totalDriving));
    }

    private static bool IsDailyRestReset(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase) &&
        activity.Duration >= DailyRestReset;

    private static bool IsDriving(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static int GetNextExtensionNumber(
        IDictionary<DateTime, int> extendedDrivingDaysByWeek,
        DateTime weekStart)
    {
        if (!extendedDrivingDaysByWeek.TryGetValue(weekStart, out var usedExtensions))
        {
            usedExtensions = 0;
        }

        var extensionNumber = usedExtensions + 1;
        extendedDrivingDaysByWeek[weekStart] = extensionNumber;

        return extensionNumber;
    }

    private static string BuildMessage(
        TimeSpan totalDriving,
        bool exceededExtendedLimit)
    {
        var formattedDuration = FormatDuration(totalDriving);

        return exceededExtendedLimit
            ? $"Dzienny czas jazdy wyniosl {formattedDuration} i przekroczyl limit 10 godzin."
            : $"Dzienny czas jazdy wyniosl {formattedDuration} i przekroczyl limit dwoch wydluzen do 10 godzin w tygodniu.";
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static DateTime GetIsoWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);

        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }

    private static DateTime Max(DateTime? left, DateTime right) =>
        !left.HasValue || right > left.Value ? right : left.Value;

    private sealed record DailyDrivingSummary(
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        DateTime WeekStartUtc,
        TimeSpan TotalDriving);
}
