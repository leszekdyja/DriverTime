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

        var normalizedTimeline = WeeklyDrivingTimelineHelper.GetMergedDrivingTimeline(timeline)
            .Concat(timeline.Where(activity => !IsDriving(activity)))
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();

        var trace = CreateTrace(normalizedTimeline);
        var continuousDriving = TimeSpan.Zero;
        var maxContinuousDriving = TimeSpan.Zero;
        var drivingSegments = 0;
        var resettingBreaks = 0;
        var pendingFirstSplitBreak = TimeSpan.Zero;
        DateTime? continuousStartUtc = null;
        var diagnosticSegments = new List<Dictionary<string, object>>();
        var debugTrace = new List<string>();
        var stepOrder = 1;

        AddTraceStep(
            trace,
            ref stepOrder,
            normalizedTimeline.FirstOrDefault()?.StartUtc,
            $"Rozpocz?to analiz? regu?y przerwy po {FormatDuration(ContinuousDrivingLimit)} jazdy.",
            counterMinutes: 0,
            isResetPoint: false,
            isViolationPoint: false,
            note: "Silnik zaczyna liczy? jazd? ci?g?? od pierwszego analizowanego segmentu.");

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
                    var note = $"Segment jazdy zwi?kszy? licznik do {FormatDuration(continuousDriving)} i przekroczy? limit.";
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
                    AddTraceSegmentAndStep(
                        trace,
                        ref stepOrder,
                        activity,
                        normalizedActivityType,
                        continuousDriving,
                        restCandidateMinutes: null,
                        isResetPoint: false,
                        isViolationPoint: true,
                        note: note,
                        stepDescription: $"Dodano segment jazdy {FormatDuration(activity.Duration)} i wykryto przekroczenie limitu.");

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
                        debugTrace,
                        trace);

                    ResetDrivingPeriod(
                        ref continuousDriving,
                        ref continuousStartUtc,
                        ref pendingFirstSplitBreak);
                }
                else
                {
                    var note = $"Segment jazdy zwi?kszy? licznik do {FormatDuration(continuousDriving)}.";
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
                    AddTraceSegmentAndStep(
                        trace,
                        ref stepOrder,
                        activity,
                        normalizedActivityType,
                        continuousDriving,
                        restCandidateMinutes: null,
                        isResetPoint: false,
                        isViolationPoint: false,
                        note: note,
                        stepDescription: $"Dodano segment jazdy {FormatDuration(activity.Duration)}.");
                }

                continue;
            }

            if (!IsQualifyingBreakActivity(activity))
            {
                var note = "Aktywno?? nie liczy si? jako przerwa i nie resetuje licznika jazdy ci?g?ej.";
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
                AddTraceSegmentAndStep(
                    trace,
                    ref stepOrder,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    restCandidateMinutes: null,
                    isResetPoint: false,
                    isViolationPoint: false,
                    note: note,
                    stepDescription: $"Dodano segment {normalizedActivityType} bez wp?ywu na licznik jazdy.");
                continue;
            }

            if (activity.Duration >= RequiredBreak)
            {
                resettingBreaks++;
                ResetDrivingPeriod(
                    ref continuousDriving,
                    ref continuousStartUtc,
                    ref pendingFirstSplitBreak);

                var note = "Przerwa lub odpoczynek co najmniej 45 min resetuje licznik jazdy ci?g?ej.";
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
                AddTraceSegmentAndStep(
                    trace,
                    ref stepOrder,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    restCandidateMinutes: ToRoundedMinutes(activity.Duration),
                    isResetPoint: true,
                    isViolationPoint: false,
                    note: note,
                    stepDescription: $"Dodano przerw?/odpoczynek {FormatDuration(activity.Duration)} i zresetowano licznik jazdy.");
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

                var note = "Druga cz??? przerwy dzielonej domyka uk?ad 15 + 30 min i resetuje licznik.";
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
                AddTraceSegmentAndStep(
                    trace,
                    ref stepOrder,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    restCandidateMinutes: ToRoundedMinutes(activity.Duration),
                    isResetPoint: true,
                    isViolationPoint: false,
                    note: note,
                    stepDescription: $"Dodano drug? cz??? przerwy dzielonej {FormatDuration(activity.Duration)} i zresetowano licznik jazdy.");
                continue;
            }

            if (pendingFirstSplitBreak < FirstSplitBreak && activity.Duration >= FirstSplitBreak)
            {
                pendingFirstSplitBreak = activity.Duration;

                var note = $"Segment przyj?ty jako pierwsza cz??? przerwy dzielonej: {ToRoundedMinutes(pendingFirstSplitBreak)} min.";
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
                AddTraceSegmentAndStep(
                    trace,
                    ref stepOrder,
                    activity,
                    normalizedActivityType,
                    continuousDriving,
                    restCandidateMinutes: ToRoundedMinutes(activity.Duration),
                    isResetPoint: false,
                    isViolationPoint: false,
                    note: note,
                    stepDescription: $"Dodano pierwsz? cz??? przerwy dzielonej {FormatDuration(activity.Duration)}.");
                continue;
            }

            var shortBreakNote = "Przerwa jest za kr?tka, aby zresetowa? licznik albo domkn?? przerw? dzielon?.";
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
            AddTraceSegmentAndStep(
                trace,
                ref stepOrder,
                activity,
                normalizedActivityType,
                continuousDriving,
                restCandidateMinutes: ToRoundedMinutes(activity.Duration),
                isResetPoint: false,
                isViolationPoint: false,
                note: shortBreakNote,
                stepDescription: $"Dodano przerw?/odpoczynek {FormatDuration(activity.Duration)}, ale segment nie resetuje licznika.");
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
        IReadOnlyList<string> debugTrace,
        RuleExecutionTrace trace)
    {
        var continuousDrivingMinutes = (long)Math.Round(continuousDriving.TotalMinutes);
        var receivedBreakMinutes = (long)Math.Round(receivedBreak.TotalMinutes);
        var exceededMinutes = Math.Max(continuousDrivingMinutes - LimitMinutes, 0);
        var lastSegment = diagnosticSegments.LastOrDefault();
        var violationTrace = CloneTrace(trace);
        violationTrace.DetectedAtUtc = periodEndUtc;
        violationTrace.Summary = $"Po analizie segment?w kierowca prowadzi? {FormatDuration(continuousDriving)} bez wymaganej przerwy {FormatDuration(RequiredBreak)}. Limit {FormatDuration(ContinuousDrivingLimit)} zosta? przekroczony o {FormatDuration(TimeSpan.FromMinutes(exceededMinutes))}.";
        violationTrace.Metrics["Limit jazdy"] = FormatDuration(ContinuousDrivingLimit);
        violationTrace.Metrics["Wymagana przerwa"] = FormatDuration(RequiredBreak);
        violationTrace.Metrics["Jazda bez poprawnej przerwy"] = FormatDuration(continuousDriving);
        violationTrace.Metrics["Przekroczenie"] = FormatDuration(TimeSpan.FromMinutes(exceededMinutes));

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
            ExecutionTrace = violationTrace,
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
                ["drivingCounterAfterSegment"] = GetLong(lastSegment, "drivingCounterAfterSegment"),
                ["firstSplitBreakAccepted"] = GetBool(lastSegment, "firstSplitBreakAccepted"),
                ["firstSplitBreakMinutes"] = GetLong(lastSegment, "firstSplitBreakMinutes"),
                ["secondSplitBreakAccepted"] = GetBool(lastSegment, "secondSplitBreakAccepted"),
                ["splitBreakCompleted"] = GetBool(lastSegment, "splitBreakCompleted"),
                ["resetReason"] = GetString(lastSegment, "resetReason"),
                ["violationDetectedAt"] = periodEndUtc,
                ["debugTrace"] = LimitDebugTrace(debugTrace)
            }
        });
    }

    private static RuleExecutionTrace CreateTrace(IReadOnlyList<TimelineActivity> normalizedTimeline)
    {
        return new RuleExecutionTrace
        {
            RuleCode = RuleCode,
            RuleName = "Przerwa po 4 godz. 30 min jazdy",
            AnalysisWindowStartUtc = normalizedTimeline.Count > 0 ? normalizedTimeline.Min(x => x.StartUtc) : null,
            AnalysisWindowEndUtc = normalizedTimeline.Count > 0 ? normalizedTimeline.Max(x => x.EndUtc) : null,
            IsEstimated = false,
            Summary = string.Empty,
            Metrics = new Dictionary<string, string>
            {
                ["Limit jazdy"] = FormatDuration(ContinuousDrivingLimit),
                ["Wymagana przerwa"] = FormatDuration(RequiredBreak)
            }
        };
    }

    private static RuleExecutionTrace CloneTrace(RuleExecutionTrace trace)
    {
        return new RuleExecutionTrace
        {
            RuleCode = trace.RuleCode,
            RuleName = trace.RuleName,
            AnalysisWindowStartUtc = trace.AnalysisWindowStartUtc,
            AnalysisWindowEndUtc = trace.AnalysisWindowEndUtc,
            DetectedAtUtc = trace.DetectedAtUtc,
            IsEstimated = trace.IsEstimated,
            Summary = trace.Summary,
            Metrics = trace.Metrics.ToDictionary(x => x.Key, x => x.Value),
            Steps = trace.Steps.Select(x => new RuleExecutionTraceStep
            {
                Order = x.Order,
                TimestampUtc = x.TimestampUtc,
                Description = x.Description,
                CounterMinutes = x.CounterMinutes,
                IsResetPoint = x.IsResetPoint,
                IsViolationPoint = x.IsViolationPoint,
                Note = x.Note
            }).ToList(),
            Segments = trace.Segments.Select(x => new RuleExecutionTraceSegment
            {
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                ActivityType = x.ActivityType,
                DurationMinutes = x.DurationMinutes,
                DrivingMinutesAfterSegment = x.DrivingMinutesAfterSegment,
                RestCandidateMinutes = x.RestCandidateMinutes,
                IsResetPoint = x.IsResetPoint,
                IsViolationPoint = x.IsViolationPoint,
                Note = x.Note
            }).ToList()
        };
    }

    private static void AddTraceSegmentAndStep(
        RuleExecutionTrace trace,
        ref int stepOrder,
        TimelineActivity activity,
        string normalizedActivityType,
        TimeSpan continuousDriving,
        long? restCandidateMinutes,
        bool isResetPoint,
        bool isViolationPoint,
        string note,
        string stepDescription)
    {
        trace.Segments.Add(new RuleExecutionTraceSegment
        {
            StartUtc = activity.StartUtc,
            EndUtc = activity.EndUtc,
            ActivityType = normalizedActivityType,
            DurationMinutes = ToRoundedMinutes(activity.Duration),
            DrivingMinutesAfterSegment = ToRoundedMinutes(continuousDriving),
            RestCandidateMinutes = restCandidateMinutes,
            IsResetPoint = isResetPoint,
            IsViolationPoint = isViolationPoint,
            Note = note
        });

        AddTraceStep(
            trace,
            ref stepOrder,
            activity.EndUtc,
            stepDescription,
            ToRoundedMinutes(continuousDriving),
            isResetPoint,
            isViolationPoint,
            note);
    }

    private static void AddTraceStep(
        RuleExecutionTrace trace,
        ref int stepOrder,
        DateTime? timestampUtc,
        string description,
        long? counterMinutes,
        bool isResetPoint,
        bool isViolationPoint,
        string note)
    {
        trace.Steps.Add(new RuleExecutionTraceStep
        {
            Order = stepOrder++,
            TimestampUtc = timestampUtc,
            Description = description,
            CounterMinutes = counterMinutes,
            IsResetPoint = isResetPoint,
            IsViolationPoint = isViolationPoint,
            Note = note
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

    private static List<string> LimitDebugTrace(IReadOnlyList<string> debugTrace)
    {
        return debugTrace
            .TakeLast(20)
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
