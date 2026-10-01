using System.Globalization;
using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class WeeklyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "WEEKLY_DRIVING_LIMIT";
    private const long WeeklyLimitMinutes = 56 * 60;
    private static readonly TimeSpan WeeklyLimit = TimeSpan.FromMinutes(WeeklyLimitMinutes);

    public string Code => RuleCode;

    public string Name => "Weekly driving limit";

    private readonly ILogger<WeeklyDrivingLimitRule> _logger;

    public WeeklyDrivingLimitRule(ILogger<WeeklyDrivingLimitRule> logger)
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
        var weeklyDriving = weeklyDrivingSegments
            .Select(x => new WeeklyDrivingSummary(
                x.Key,
                x.Key.AddDays(7),
                x.Value,
                x.Value.Aggregate(TimeSpan.Zero, (sum, segment) => sum + segment.Duration)))
            .OrderBy(x => x.WeekStartUtc)
            .ToList();

        foreach (var week in weeklyDriving)
        {
            if (week.Duration <= WeeklyLimit)
            {
                continue;
            }

            var actualMinutes = (long)Math.Round(week.Duration.TotalMinutes);
            var exceededMinutes = actualMinutes - WeeklyLimitMinutes;

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Tygodniowy czas jazdy wyni?s? {FormatDuration(week.Duration)} i przekroczy? limit 56 godzin.",
                PeriodStartUtc = week.WeekStartUtc,
                PeriodEndUtc = week.WeekEndUtc,
                ActualMinutes = actualMinutes,
                LimitMinutes = WeeklyLimitMinutes,
                ExecutionTrace = BuildRuleExecutionTrace(week, actualMinutes, exceededMinutes),
                Metadata = new Dictionary<string, object>
                {
                    ["totalDrivingMinutes"] = actualMinutes,
                    ["limitMinutes"] = WeeklyLimitMinutes,
                    ["exceededMinutes"] = exceededMinutes
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, maxWeeklyDrivingMinutes={MaxWeeklyDrivingMinutes}, weeksOver56h={WeeksOverLimit}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weeklyDriving.Count,
            weeklyDriving.Count == 0 ? 0 : (long)Math.Round(weeklyDriving.Max(x => x.Duration.TotalMinutes)),
            weeklyDriving.Count(x => x.Duration > WeeklyLimit),
            result.Violations.Count);

        return result;
    }

    private static RuleExecutionTrace BuildRuleExecutionTrace(
        WeeklyDrivingSummary week,
        long actualMinutes,
        long exceededMinutes)
    {
        var trace = new RuleExecutionTrace
        {
            RuleCode = RuleCode,
            RuleName = "Limit jazdy tygodniowej",
            AnalysisWindowStartUtc = week.WeekStartUtc,
            AnalysisWindowEndUtc = week.WeekEndUtc,
            DetectedAtUtc = FindViolationDetectedAt(week.DrivingSegments, WeeklyLimitMinutes) ?? week.WeekEndUtc,
            IsEstimated = false,
            Summary = $"W tygodniu ISO od {FormatDateTime(week.WeekStartUtc)} do {FormatDateTime(week.WeekEndUtc)} czas jazdy wyni?s? {FormatDuration(week.Duration)}. Limit 56 godzin zosta? przekroczony o {FormatDuration(TimeSpan.FromMinutes(exceededMinutes))}.",
            Metrics = new Dictionary<string, string>
            {
                ["Tydzie? ISO"] = $"{FormatDateTime(week.WeekStartUtc)} - {FormatDateTime(week.WeekEndUtc)}",
                ["Limit jazdy"] = FormatDuration(WeeklyLimit),
                ["Czas jazdy"] = FormatDuration(week.Duration),
                ["Przekroczenie"] = FormatDuration(TimeSpan.FromMinutes(exceededMinutes)),
                ["Ko?cowa suma jazdy"] = actualMinutes.ToString(CultureInfo.InvariantCulture) + " min"
            }
        };

        var order = 1;
        AddTraceStep(
            trace,
            ref order,
            week.WeekStartUtc,
            "Start analizy tygodniowego limitu jazdy.",
            counterMinutes: 0,
            isViolationPoint: false,
            note: "Analiza obejmuje tydzie? ISO od poniedzia?ku 00:00 UTC do kolejnego poniedzia?ku 00:00 UTC.");

        var drivingCounter = 0L;
        foreach (var segment in week.DrivingSegments.OrderBy(x => x.StartUtc).ThenBy(x => x.EndUtc))
        {
            var durationMinutes = (long)Math.Round(segment.Duration.TotalMinutes);
            drivingCounter += durationMinutes;
            var isViolationPoint = drivingCounter > WeeklyLimitMinutes;
            var note = isViolationPoint
                ? "Ten segment powoduje przekroczenie tygodniowego limitu jazdy."
                : "Segment jazdy wliczony do tygodniowej sumy jazdy.";

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
                    ? $"Dodano segment jazdy {FormatDuration(segment.Duration)} i wykryto przekroczenie limitu tygodniowego."
                    : $"Dodano segment jazdy {FormatDuration(segment.Duration)}.",
                drivingCounter,
                isViolationPoint,
                note);
        }

        AddTraceStep(
            trace,
            ref order,
            trace.DetectedAtUtc,
            "Ko?cowe podsumowanie tygodniowego limitu jazdy.",
            actualMinutes,
            isViolationPoint: true,
            note: trace.Summary);

        return trace;
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

            var minutesAfterLimitAtSegmentStart = Math.Max(0, beforeSegment - limitMinutes);
            var minutesToViolation = Math.Max(0, limitMinutes - beforeSegment);

            return segment.StartUtc.AddMinutes(minutesToViolation + (minutesAfterLimitAtSegmentStart > 0 ? 0 : 1));
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

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static string FormatDateTime(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private sealed record WeeklyDrivingSummary(
        DateTime WeekStartUtc,
        DateTime WeekEndUtc,
        IReadOnlyList<TimelineActivity> DrivingSegments,
        TimeSpan Duration);
}
