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

            var totalDrivingMinutes = (long)Math.Round(period.TotalDriving.TotalMinutes);
            var limitMinutes = (long)Math.Round(limit.TotalMinutes);

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = severity,
                Description = BuildMessage(period.TotalDriving, exceededExtendedLimit),
                PeriodStartUtc = period.PeriodStartUtc,
                PeriodEndUtc = period.PeriodEndUtc,
                ActualMinutes = totalDrivingMinutes,
                LimitMinutes = limitMinutes,
                ExecutionTrace = BuildRuleExecutionTrace(period, limit, exceededMinutes, extensionNumber, exceededExtendedLimit),
                Metadata = new Dictionary<string, object>
                {
                    ["totalDrivingMinutes"] = totalDrivingMinutes,
                    ["limitMinutes"] = limitMinutes,
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


    private static RuleExecutionTrace BuildRuleExecutionTrace(
        DailyDrivingSummary period,
        TimeSpan limit,
        long exceededMinutes,
        int extensionNumber,
        bool exceededExtendedLimit)
    {
        var limitMinutes = (long)Math.Round(limit.TotalMinutes);
        var totalDrivingMinutes = (long)Math.Round(period.TotalDriving.TotalMinutes);
        var trace = new RuleExecutionTrace
        {
            RuleCode = RuleCode,
            RuleName = "Limit jazdy dziennej",
            AnalysisWindowStartUtc = period.PeriodStartUtc,
            AnalysisWindowEndUtc = period.PeriodEndUtc,
            DetectedAtUtc = period.PeriodEndUtc,
            IsEstimated = false,
            Summary = $"W analizowanej dobie kierowcy czas jazdy wyniósł {FormatDuration(period.TotalDriving)}. Limit {FormatDuration(limit)} został przekroczony o {FormatDuration(TimeSpan.FromMinutes(exceededMinutes))}.",
            Metrics = new Dictionary<string, string>
            {
                ["Okno dzienne"] = $"{FormatDateTime(period.PeriodStartUtc)} - {FormatDateTime(period.PeriodEndUtc)}",
                ["Limit jazdy"] = FormatDuration(limit),
                ["Czas jazdy"] = FormatDuration(period.TotalDriving),
                ["Przekroczenie"] = FormatDuration(TimeSpan.FromMinutes(exceededMinutes)),
                ["Numer wydłużenia w tygodniu"] = extensionNumber.ToString(CultureInfo.InvariantCulture),
                ["Rodzaj limitu"] = exceededExtendedLimit ? "10 godzin" : "9 godzin"
            }
        };

        var order = 1;
        AddTraceStep(
            trace,
            ref order,
            period.PeriodStartUtc,
            "Start analizy dziennego limitu jazdy.",
            counterMinutes: 0,
            isViolationPoint: false,
            note: "Okres kierowcy liczony od aktywności do odpoczynku dziennego minimum 9 godzin.");

        var drivingCounter = 0L;
        foreach (var segment in period.DrivingSegments.OrderBy(x => x.StartUtc).ThenBy(x => x.EndUtc))
        {
            var durationMinutes = (long)Math.Round(segment.Duration.TotalMinutes);
            drivingCounter += durationMinutes;
            var isViolationPoint = drivingCounter > limitMinutes;
            var note = isViolationPoint
                ? "Ten segment powoduje przekroczenie dziennego limitu jazdy."
                : "Segment jazdy wliczony do dziennej sumy jazdy.";

            trace.Segments.Add(new RuleExecutionTraceSegment
            {
                StartUtc = segment.StartUtc,
                EndUtc = segment.EndUtc,
                ActivityType = ActivityTypeNormalizer.Driving,
                DurationMinutes = durationMinutes,
                DrivingMinutesAfterSegment = drivingCounter,
                IsViolationPoint = isViolationPoint,
                Note = note
            });

            AddTraceStep(
                trace,
                ref order,
                segment.EndUtc,
                isViolationPoint
                    ? $"Dodano segment jazdy {FormatDuration(segment.Duration)} i wykryto przekroczenie limitu."
                    : $"Dodano segment jazdy {FormatDuration(segment.Duration)}.",
                drivingCounter,
                isViolationPoint,
                note);
        }

        AddTraceStep(
            trace,
            ref order,
            period.PeriodEndUtc,
            "Końcowe podsumowanie dziennego limitu jazdy.",
            totalDrivingMinutes,
            isViolationPoint: true,
            note: trace.Summary);

        return trace;
    }

    private static void AddTraceStep(
        RuleExecutionTrace trace,
        ref int order,
        DateTime? timestampUtc,
        string description,
        long? counterMinutes,
        bool isViolationPoint,
        string note)
    {
        trace.Steps.Add(new RuleExecutionTraceStep
        {
            Order = order++,
            TimestampUtc = timestampUtc,
            Description = description,
            CounterMinutes = counterMinutes,
            IsViolationPoint = isViolationPoint,
            Note = note
        });
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
        var drivingSegments = new List<TimelineActivity>();

        foreach (var activity in ordered)
        {
            if (periodStartUtc.HasValue &&
                previousEndUtc.HasValue &&
                activity.StartUtc - previousEndUtc.Value >= DailyRestReset)
            {
                AddPeriodIfDriving(periods, periodStartUtc, periodEndUtc, totalDriving, drivingSegments);
                periodStartUtc = null;
                periodEndUtc = null;
                totalDriving = TimeSpan.Zero;
                drivingSegments = new List<TimelineActivity>();
            }

            if (IsDailyRestReset(activity))
            {
                AddPeriodIfDriving(periods, periodStartUtc, periodEndUtc, totalDriving, drivingSegments);
                periodStartUtc = null;
                periodEndUtc = null;
                totalDriving = TimeSpan.Zero;
                drivingSegments = new List<TimelineActivity>();
                previousEndUtc = Max(previousEndUtc, activity.EndUtc);
                continue;
            }

            periodStartUtc ??= activity.StartUtc;
            periodEndUtc = Max(periodEndUtc, activity.EndUtc);
            previousEndUtc = Max(previousEndUtc, activity.EndUtc);

            if (IsDriving(activity))
            {
                totalDriving += activity.Duration;
                drivingSegments.Add(activity);
            }
        }

        AddPeriodIfDriving(periods, periodStartUtc, periodEndUtc, totalDriving, drivingSegments);

        return periods;
    }

    private static void AddPeriodIfDriving(
        ICollection<DailyDrivingSummary> periods,
        DateTime? periodStartUtc,
        DateTime? periodEndUtc,
        TimeSpan totalDriving,
        IReadOnlyList<TimelineActivity> drivingSegments)
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
            totalDriving,
            drivingSegments.ToList()));
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
            ? $"Dzienny czas jazdy wyniósł {formattedDuration} i przekroczy? limit 10 godzin."
            : $"Dzienny czas jazdy wyniósł {formattedDuration} i przekroczył limit dw?ch wyd?u?e? do 10 godzin w tygodniu.";
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static string FormatDateTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

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
        TimeSpan TotalDriving,
        IReadOnlyList<TimelineActivity> DrivingSegments);
}
