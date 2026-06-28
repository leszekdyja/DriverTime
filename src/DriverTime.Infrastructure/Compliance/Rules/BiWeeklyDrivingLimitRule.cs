using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class BiWeeklyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "BI_WEEKLY_DRIVING_LIMIT";
    private const long BiWeeklyLimitMinutes = 90 * 60;
    private static readonly TimeSpan BiWeeklyLimit = TimeSpan.FromMinutes(BiWeeklyLimitMinutes);

    public string Code => RuleCode;

    public string Name => "Bi-weekly driving limit";

    private readonly ILogger<BiWeeklyDrivingLimitRule> _logger;

    public BiWeeklyDrivingLimitRule(ILogger<BiWeeklyDrivingLimitRule> logger)
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

        var weeklyDrivingSegments = WeeklyDrivingTimelineHelper.GetDrivingSegmentsByIsoWeek(timeline);
        var weeklyDriving = weeklyDrivingSegments.ToDictionary(
            x => x.Key,
            x => x.Value.Aggregate(TimeSpan.Zero, (sum, segment) => sum + segment.Duration));

        if (weeklyDriving.Count == 0)
        {
            LogSummary(driverId, weekCount: 0, pairCount: 0, maxBiWeeklyDriving: TimeSpan.Zero, pairsOverLimit: 0, violationCount: 0);
            return result;
        }

        var weekStarts = weeklyDriving.Keys.OrderBy(x => x).ToList();
        var firstWeekStart = weekStarts[0];
        var lastWeekStart = weekStarts[^1];
        var pairCount = 0;
        var pairsOverLimit = 0;
        var maxBiWeeklyDriving = TimeSpan.Zero;

        for (var weekStart = firstWeekStart; weekStart <= lastWeekStart; weekStart = weekStart.AddDays(7))
        {
            var secondWeekStart = weekStart.AddDays(7);
            var firstWeekDriving = weeklyDriving.GetValueOrDefault(weekStart);
            var secondWeekDriving = weeklyDriving.GetValueOrDefault(secondWeekStart);
            var firstWeekSegments = weeklyDrivingSegments.GetValueOrDefault(weekStart) ?? Array.Empty<TimelineActivity>();
            var secondWeekSegments = weeklyDrivingSegments.GetValueOrDefault(secondWeekStart) ?? Array.Empty<TimelineActivity>();
            var duration = firstWeekDriving + secondWeekDriving;

            pairCount++;
            maxBiWeeklyDriving = Max(maxBiWeeklyDriving, duration);

            if (duration <= BiWeeklyLimit)
            {
                continue;
            }

            pairsOverLimit++;
            var actualMinutes = (long)Math.Round(duration.TotalMinutes);
            var exceededMinutes = actualMinutes - BiWeeklyLimitMinutes;
            var summary = new BiWeeklyDrivingSummary(
                weekStart,
                weekStart.AddDays(14),
                firstWeekDriving,
                secondWeekDriving,
                firstWeekSegments,
                secondWeekSegments,
                duration);

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Czas jazdy w dw?ch kolejnych tygodniach wyni?s? {FormatDuration(duration)} i przekroczy? limit 90 godzin.",
                PeriodStartUtc = weekStart,
                PeriodEndUtc = weekStart.AddDays(14),
                ActualMinutes = actualMinutes,
                LimitMinutes = BiWeeklyLimitMinutes,
                ExecutionTrace = BuildRuleExecutionTrace(summary, actualMinutes, exceededMinutes),
                Metadata = new Dictionary<string, object>
                {
                    ["totalDrivingMinutes"] = actualMinutes,
                    ["limitMinutes"] = BiWeeklyLimitMinutes,
                    ["exceededMinutes"] = exceededMinutes,
                    ["firstWeekDrivingMinutes"] = (long)Math.Round(firstWeekDriving.TotalMinutes),
                    ["secondWeekDrivingMinutes"] = (long)Math.Round(secondWeekDriving.TotalMinutes)
                }
            });
        }

        LogSummary(
            driverId,
            weeklyDriving.Count,
            pairCount,
            maxBiWeeklyDriving,
            pairsOverLimit,
            result.Violations.Count);

        return result;
    }

    private static RuleExecutionTrace BuildRuleExecutionTrace(
        BiWeeklyDrivingSummary period,
        long actualMinutes,
        long exceededMinutes)
    {
        var firstWeekMinutes = (long)Math.Round(period.FirstWeekDriving.TotalMinutes);
        var secondWeekMinutes = (long)Math.Round(period.SecondWeekDriving.TotalMinutes);
        var trace = new RuleExecutionTrace
        {
            RuleCode = RuleCode,
            RuleName = "Limit jazdy w dw?ch kolejnych tygodniach",
            AnalysisWindowStartUtc = period.PeriodStartUtc,
            AnalysisWindowEndUtc = period.PeriodEndUtc,
            DetectedAtUtc = FindViolationDetectedAt(period.AllSegments, BiWeeklyLimitMinutes) ?? period.PeriodEndUtc,
            IsEstimated = false,
            Summary = $"W analizowanej parze tygodni ISO od {FormatDateTime(period.PeriodStartUtc)} do {FormatDateTime(period.PeriodEndUtc)} czas jazdy wyni?s? {FormatDuration(period.Duration)}. Limit 90 godzin zosta? przekroczony o {FormatDuration(TimeSpan.FromMinutes(exceededMinutes))}.",
            Metrics = new Dictionary<string, string>
            {
                ["Okres dwutygodniowy"] = $"{FormatDateTime(period.PeriodStartUtc)} - {FormatDateTime(period.PeriodEndUtc)}",
                ["Limit jazdy"] = FormatDuration(BiWeeklyLimit),
                ["Czas jazdy"] = FormatDuration(period.Duration),
                ["Przekroczenie"] = FormatDuration(TimeSpan.FromMinutes(exceededMinutes)),
                ["Pierwszy tydzie?"] = FormatDuration(period.FirstWeekDriving),
                ["Drugi tydzie?"] = FormatDuration(period.SecondWeekDriving),
                ["Ko?cowa suma jazdy"] = actualMinutes.ToString(CultureInfo.InvariantCulture) + " min"
            }
        };

        var order = 1;
        AddTraceStep(
            trace,
            ref order,
            period.PeriodStartUtc,
            "Start analizy limitu jazdy w dw?ch kolejnych tygodniach.",
            counterMinutes: 0,
            isViolationPoint: false,
            note: "Analiza obejmuje dwie nast?puj?ce po sobie jednostki tygodnia ISO.");

        var drivingCounter = 0L;
        AddSegmentsToTrace(trace, ref order, period.FirstWeekSegments, "pierwszym tygodniu", ref drivingCounter);
        AddTraceStep(
            trace,
            ref order,
            period.PeriodStartUtc.AddDays(7),
            $"Podsumowanie pierwszego tygodnia: {FormatDuration(period.FirstWeekDriving)} jazdy.",
            firstWeekMinutes,
            isViolationPoint: false,
            note: "Suma jazdy z pierwszego tygodnia okresu dwutygodniowego.");
        AddSegmentsToTrace(trace, ref order, period.SecondWeekSegments, "drugim tygodniu", ref drivingCounter);
        AddTraceStep(
            trace,
            ref order,
            period.PeriodEndUtc,
            $"Podsumowanie drugiego tygodnia: {FormatDuration(period.SecondWeekDriving)} jazdy.",
            firstWeekMinutes + secondWeekMinutes,
            isViolationPoint: false,
            note: "Suma jazdy z obu tygodni przed ocen? limitu 90 godzin.");

        AddTraceStep(
            trace,
            ref order,
            trace.DetectedAtUtc,
            "Ko?cowe podsumowanie limitu jazdy w dw?ch kolejnych tygodniach.",
            actualMinutes,
            isViolationPoint: true,
            note: trace.Summary);

        return trace;
    }

    private static void AddSegmentsToTrace(
        RuleExecutionTrace trace,
        ref int order,
        IReadOnlyList<TimelineActivity> segments,
        string weekLabel,
        ref long drivingCounter)
    {
        foreach (var segment in segments.OrderBy(x => x.StartUtc).ThenBy(x => x.EndUtc))
        {
            var durationMinutes = (long)Math.Round(segment.Duration.TotalMinutes);
            drivingCounter += durationMinutes;
            var isViolationPoint = drivingCounter > BiWeeklyLimitMinutes;
            var note = isViolationPoint
                ? $"Ten segment w {weekLabel} powoduje przekroczenie limitu 90 godzin."
                : $"Segment jazdy w {weekLabel} wliczony do sumy dwutygodniowej.";

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
                    ? $"Dodano segment jazdy {FormatDuration(segment.Duration)} z {weekLabel} i wykryto przekroczenie limitu dwutygodniowego."
                    : $"Dodano segment jazdy {FormatDuration(segment.Duration)} z {weekLabel}.",
                drivingCounter,
                isViolationPoint,
                note);
        }
    }

    private static DateTime? FindViolationDetectedAt(
        IReadOnlyList<TimelineActivity> drivingSegments,
        long limitMinutes)
    {
        var drivingCounter = 0L;

        foreach (var segment in drivingSegments.OrderBy(x => x.StartUtc).ThenBy(x => x.EndUtc))
        {
            var durationMinutes = (long)Math.Round(segment.Duration.TotalMinutes);
            var beforeSegment = drivingCounter;
            drivingCounter += durationMinutes;

            if (drivingCounter <= limitMinutes)
            {
                continue;
            }

            var minutesToViolation = Math.Max(0, limitMinutes - beforeSegment);

            return segment.StartUtc.AddMinutes(minutesToViolation + 1);
        }

        return null;
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

    private void LogSummary(
        Guid driverId,
        int weekCount,
        int pairCount,
        TimeSpan maxBiWeeklyDriving,
        int pairsOverLimit,
        int violationCount)
    {
        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, consecutivePairs={ConsecutivePairs}, maxBiWeeklyDrivingMinutes={MaxBiWeeklyDrivingMinutes}, pairsOver90h={PairsOverLimit}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weekCount,
            pairCount,
            (long)Math.Round(maxBiWeeklyDriving.TotalMinutes),
            pairsOverLimit,
            violationCount);
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static string FormatDateTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;

    private sealed record BiWeeklyDrivingSummary(
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        TimeSpan FirstWeekDriving,
        TimeSpan SecondWeekDriving,
        IReadOnlyList<TimelineActivity> FirstWeekSegments,
        IReadOnlyList<TimelineActivity> SecondWeekSegments,
        TimeSpan Duration)
    {
        public IReadOnlyList<TimelineActivity> AllSegments { get; } = FirstWeekSegments
            .Concat(SecondWeekSegments)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
    }
}
