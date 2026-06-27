using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class ContinuousDrivingBreakRule : IComplianceRule
{
    private const string RuleCode = "CONTINUOUS_DRIVING_BREAK";
    private const long LimitMinutes = 270;
    private const long RequiredBreakMinutes = 45;
    private const long FirstSplitBreakMinutes = 15;
    private const long SecondSplitBreakMinutes = 30;
    private const string ResetReasonNone = "NONE";
    private const string ResetReasonFortyFiveMinuteBreak = "45_MIN_BREAK";
    private const string ResetReasonSplitBreak = "SPLIT_15_30";
    private static readonly TimeSpan ContinuousDrivingLimit = TimeSpan.FromMinutes(LimitMinutes);
    private static readonly TimeSpan RequiredBreak = TimeSpan.FromMinutes(RequiredBreakMinutes);
    private static readonly TimeSpan FirstSplitBreak = TimeSpan.FromMinutes(FirstSplitBreakMinutes);
    private static readonly TimeSpan SecondSplitBreak = TimeSpan.FromMinutes(SecondSplitBreakMinutes);

    public string Code => RuleCode;

    public string Name => "Continuous driving break";

    private readonly ILogger<ContinuousDrivingBreakRule> _logger;

    public ContinuousDrivingBreakRule(ILogger<ContinuousDrivingBreakRule> logger)
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

        var continuousDriving = TimeSpan.Zero;
        var maxContinuousDriving = TimeSpan.Zero;
        var drivingSegments = 0;
        var resettingBreaks = 0;
        var pendingFirstSplitBreak = TimeSpan.Zero;
        DateTime? continuousStartUtc = null;
        var diagnosticSegments = new List<Dictionary<string, object>>();
        var debugTrace = new List<string>();

        var normalizedTimeline = WeeklyDrivingTimelineHelper.GetMergedDrivingTimeline(timeline)
            .Concat(timeline.Where(activity => !IsDriving(activity)))
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        foreach (var activity in normalizedTimeline)
        {
            var normalizedActivityType = ActivityTypeNormalizer.Normalize(activity.ActivityType);

            if (normalizedActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase))
            {
                drivingSegments++;
                continuousStartUtc ??= activity.StartUtc;
                continuousDriving += activity.Duration;
                maxContinuousDriving = Max(maxContinuousDriving, continuousDriving);

                if (continuousDriving > ContinuousDrivingLimit)
                {
                    AddDiagnosticSegment(
                        diagnosticSegments,
                        debugTrace,
                        activity,
                        normalizedActivityType,
                        continuousDriving,
                        pendingFirstSplitBreak,
                        secondSplitBreakAccepted: false,
                        splitBreakCompleted: false,
                        ResetReasonNone,
                        activity.EndUtc,
                        $"DRIVING increased continuous driving to {FormatDuration(continuousDriving)} and triggered violation.");

                    AddViolation(
                        result,
                        continuousStartUtc.Value,
                        activity.EndUtc,
                        continuousDriving,
                        pendingFirstSplitBreak,
                        pendingFirstSplitBreak >= FirstSplitBreak
                            ? "Missing second split break of at least 30 minutes"
                            : "Missing required break",
                        diagnosticSegments,
                        debugTrace);

                    ResetDrivingPeriod(
                        ref continuousDriving,
                        ref continuousStartUtc,
                        ref pendingFirstSplitBreak);
                }
                else
                {
                    AddDiagnosticSegment(
                        diagnosticSegments,
                        debugTrace,
                        activity,
                        normalizedActivityType,
                        continuousDriving,
                        pendingFirstSplitBreak,
                        secondSplitBreakAccepted: false,
                        splitBreakCompleted: false,
                        ResetReasonNone,
                        violationDetectedAt: null,
                        $"DRIVING increased continuous driving to {FormatDuration(continuousDriving)}.");
                }

                continue;
            }

            if (!IsQualifyingBreakActivity(activity))
            {
                AddDiagnosticSegment(
                    diagnosticSegments,
                    debugTrace,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    pendingFirstSplitBreak,
                    secondSplitBreakAccepted: false,
                    splitBreakCompleted: false,
                    ResetReasonNone,
                    violationDetectedAt: null,
                    $"{normalizedActivityType} does not count as break and does not reset continuous driving.");
                continue;
            }

            if (activity.Duration >= RequiredBreak)
            {
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak);

                AddDiagnosticSegment(
                    diagnosticSegments,
                    debugTrace,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    pendingFirstSplitBreak,
                    secondSplitBreakAccepted: false,
                    splitBreakCompleted: false,
                    ResetReasonFortyFiveMinuteBreak,
                    violationDetectedAt: null,
                    $"{normalizedActivityType} lasted at least 45 minutes and reset continuous driving.");
                continue;
            }

            if (pendingFirstSplitBreak >= FirstSplitBreak && activity.Duration >= SecondSplitBreak)
            {
                var acceptedFirstSplitBreak = pendingFirstSplitBreak;
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak);

                AddDiagnosticSegment(
                    diagnosticSegments,
                    debugTrace,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    acceptedFirstSplitBreak,
                    secondSplitBreakAccepted: true,
                    splitBreakCompleted: true,
                    ResetReasonSplitBreak,
                    violationDetectedAt: null,
                    $"{normalizedActivityType} completed valid 15 + 30 split break and reset continuous driving.");
                continue;
            }

            if (pendingFirstSplitBreak < FirstSplitBreak && activity.Duration >= FirstSplitBreak)
            {
                pendingFirstSplitBreak = activity.Duration;

                AddDiagnosticSegment(
                    diagnosticSegments,
                    debugTrace,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    pendingFirstSplitBreak,
                    secondSplitBreakAccepted: false,
                    splitBreakCompleted: false,
                    ResetReasonNone,
                    violationDetectedAt: null,
                    $"{normalizedActivityType} accepted as first split break part with {ToRoundedMinutes(pendingFirstSplitBreak)} minutes.");
                continue;
            }

            AddDiagnosticSegment(
                diagnosticSegments,
                debugTrace,
                activity,
                normalizedActivityType,
                continuousDriving,
                pendingFirstSplitBreak,
                secondSplitBreakAccepted: false,
                splitBreakCompleted: false,
                ResetReasonNone,
                violationDetectedAt: null,
                $"{normalizedActivityType} break segment was too short to reset or complete split break.");
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: drivingSegments={DrivingSegments}, resettingBreaks={ResettingBreaks}, maxContinuousDrivingMinutes={MaxContinuousDrivingMinutes}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            drivingSegments,
            resettingBreaks,
            (long)Math.Round(maxContinuousDriving.TotalMinutes),
            result.Violations.Count);

        return result;
    }

    private static bool IsDriving(TimelineActivity activity) =>
        ActivityTypeNormalizer.Normalize(activity.ActivityType)
            .Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static bool IsQualifyingBreakActivity(TimelineActivity activity) =>
        ActivityTypeNormalizer.Normalize(activity.ActivityType) is ActivityTypeNormalizer.Rest or ActivityTypeNormalizer.Availability;

    private static void AddViolation(
        ComplianceRuleResult result,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        TimeSpan continuousDriving,
        TimeSpan receivedBreak,
        string breakType,
        IReadOnlyList<Dictionary<string, object>> diagnosticSegments,
        IReadOnlyList<string> debugTrace)
    {
        var continuousDrivingMinutes = (long)Math.Round(continuousDriving.TotalMinutes);
        var receivedBreakMinutes = (long)Math.Round(receivedBreak.TotalMinutes);
        var exceededMinutes = Math.Max(continuousDrivingMinutes - LimitMinutes, 0);
        var lastSegment = diagnosticSegments.LastOrDefault();

        result.Violations.Add(new ComplianceViolationCandidate
        {
            Code = RuleCode,
            RuleName = "Continuous driving break",
            Severity = "HIGH",
            Description = $"Continuous driving lasted {FormatDuration(continuousDriving)} without the required 45 minute break or valid 15 + 30 split break.",
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            ActualMinutes = continuousDrivingMinutes,
            LimitMinutes = LimitMinutes,
            Metadata = new Dictionary<string, object>
            {
                ["continuousDrivingMinutes"] = continuousDrivingMinutes,
                ["limitMinutes"] = LimitMinutes,
                ["requiredBreakMinutes"] = RequiredBreakMinutes,
                ["receivedBreakMinutes"] = receivedBreakMinutes,
                ["exceededMinutes"] = exceededMinutes,
                ["breakType"] = breakType,
                ["splitBreakFirstPartMinutes"] = FirstSplitBreakMinutes,
                ["splitBreakSecondPartMinutes"] = SecondSplitBreakMinutes,
                ["analyzedSegments"] = CloneSegments(diagnosticSegments),
                ["drivingCounterAfterSegment"] = GetLong(lastSegment, "drivingCounterAfterSegment"),
                ["firstSplitBreakAccepted"] = GetBool(lastSegment, "firstSplitBreakAccepted"),
                ["firstSplitBreakMinutes"] = GetLong(lastSegment, "firstSplitBreakMinutes"),
                ["secondSplitBreakAccepted"] = GetBool(lastSegment, "secondSplitBreakAccepted"),
                ["splitBreakCompleted"] = GetBool(lastSegment, "splitBreakCompleted"),
                ["resetReason"] = GetString(lastSegment, "resetReason"),
                ["violationDetectedAt"] = periodEndUtc,
                ["debugTrace"] = debugTrace.ToList()
            }
        });
    }

    private static void AddDiagnosticSegment(
        ICollection<Dictionary<string, object>> diagnosticSegments,
        ICollection<string> debugTrace,
        TimelineActivity activity,
        string normalizedActivityType,
        TimeSpan continuousDriving,
        TimeSpan firstSplitBreak,
        bool secondSplitBreakAccepted,
        bool splitBreakCompleted,
        string resetReason,
        DateTime? violationDetectedAt,
        string traceMessage)
    {
        var firstSplitBreakMinutes = ToRoundedMinutes(firstSplitBreak);

        diagnosticSegments.Add(new Dictionary<string, object>
        {
            ["StartUtc"] = activity.StartUtc,
            ["EndUtc"] = activity.EndUtc,
            ["DurationMinutes"] = ToRoundedMinutes(activity.Duration),
            ["ActivityType"] = normalizedActivityType,
            ["drivingCounterAfterSegment"] = ToRoundedMinutes(continuousDriving),
            ["firstSplitBreakAccepted"] = firstSplitBreakMinutes >= FirstSplitBreakMinutes,
            ["firstSplitBreakMinutes"] = firstSplitBreakMinutes,
            ["secondSplitBreakAccepted"] = secondSplitBreakAccepted,
            ["splitBreakCompleted"] = splitBreakCompleted,
            ["resetReason"] = resetReason,
            ["violationDetectedAt"] = violationDetectedAt.HasValue ? violationDetectedAt.Value : string.Empty
        });

        debugTrace.Add($"{activity.StartUtc:o}..{activity.EndUtc:o}: {traceMessage}");
    }

    private static List<Dictionary<string, object>> CloneSegments(
        IReadOnlyList<Dictionary<string, object>> diagnosticSegments)
    {
        return diagnosticSegments
            .Select(segment => segment.ToDictionary(x => x.Key, x => x.Value))
            .ToList();
    }

    private static long GetLong(
        IReadOnlyDictionary<string, object>? values,
        string key)
    {
        return values is not null && values.TryGetValue(key, out var value) && value is long longValue
            ? longValue
            : 0;
    }

    private static bool GetBool(
        IReadOnlyDictionary<string, object>? values,
        string key)
    {
        return values is not null && values.TryGetValue(key, out var value) && value is bool boolValue && boolValue;
    }

    private static string GetString(
        IReadOnlyDictionary<string, object>? values,
        string key)
    {
        return values is not null && values.TryGetValue(key, out var value) && value is string stringValue
            ? stringValue
            : ResetReasonNone;
    }

    private static void ResetDrivingPeriod(
        ref TimeSpan continuousDriving,
        ref DateTime? continuousStartUtc,
        ref TimeSpan pendingFirstSplitBreak)
    {
        continuousDriving = TimeSpan.Zero;
        continuousStartUtc = null;
        pendingFirstSplitBreak = TimeSpan.Zero;
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static long ToRoundedMinutes(TimeSpan duration) =>
        (long)Math.Round(duration.TotalMinutes);

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
