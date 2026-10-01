using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace DriverTime.Infrastructure.Compliance;

public class ComplianceEngineService : IComplianceEngineService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ITimelineBuilderService _timelineBuilder;
    private readonly IEnumerable<IComplianceRule> _rules;
    private readonly IEnumerable<ICountryEntryComplianceRule> _countryEntryRules;
    private readonly ILogger<ComplianceEngineService> _logger;

    public ComplianceEngineService(
        DriverTimeDbContext dbContext,
        ITimelineBuilderService timelineBuilder,
        IEnumerable<IComplianceRule> rules,
        IEnumerable<ICountryEntryComplianceRule> countryEntryRules,
        ILogger<ComplianceEngineService> logger)
    {
        _dbContext = dbContext;
        _timelineBuilder = timelineBuilder;
        _rules = rules;
        _countryEntryRules = countryEntryRules;
        _logger = logger;
    }

    public async Task<CompliancePreviewResponseDto?> PreviewForDriverAsync(
        Guid companyId,
        Guid driverId,
        bool includeTimeline = false,
        DateTime? rangeStartUtc = null,
        DateTime? rangeEndUtc = null,
        CancellationToken cancellationToken = default)
    {
        var analysisRange = ComplianceAnalysisRange.Resolve(
            DateTime.UtcNow,
            rangeStartUtc,
            rangeEndUtc);

        var timeline = await _timelineBuilder.BuildForDriverAsync(
            companyId,
            driverId,
            analysisRange.QueryStartUtc,
            analysisRange.QueryEndUtc,
            cancellationToken);

        if (timeline is null)
        {
            return null;
        }

        var countryEntries = await BuildCountryEntriesForDriverAsync(
            companyId,
            driverId,
            analysisRange.QueryStartUtc,
            analysisRange.QueryEndUtc,
            cancellationToken);

        _logger.LogInformation(
            "Compliance preview timeline built for driver {DriverId}. Activities count={Count}.",
            driverId,
            timeline.Count);

        var violationCandidates = new List<ComplianceViolationCandidate>();

        foreach (var rule in _rules)
        {
            var ruleResult = rule.Evaluate(driverId, timeline);
            var ruleViolations = ruleResult.Violations;

            _logger.LogInformation(
                "Compliance rule {Rule} returned {Count} violations for driver {DriverId}.",
                rule.Code,
                ruleViolations.Count,
                driverId);

            violationCandidates.AddRange(ruleViolations);
        }

        foreach (var rule in _countryEntryRules)
        {
            var ruleResult = rule.Evaluate(driverId, timeline, countryEntries);
            var ruleViolations = ruleResult.Violations;

            _logger.LogInformation(
                "Compliance country-entry rule {Rule} returned {Count} violations for driver {DriverId}.",
                rule.Code,
                ruleViolations.Count,
                driverId);

            violationCandidates.AddRange(ruleViolations);
        }

        var violations = violationCandidates
            .Where(x => analysisRange.IntersectsVisibleRange(
                x.PeriodStartUtc,
                x.PeriodEndUtc))
            .OrderBy(x => x.PeriodStartUtc)
            .ThenBy(x => x.Code)
            .Select(x => new ComplianceViolationPreviewDto
            {
                Code = x.Code,
                RuleName = x.RuleName,
                Severity = x.Severity,
                Description = x.Description,
                PeriodStartUtc = x.PeriodStartUtc,
                PeriodEndUtc = x.PeriodEndUtc,
                ActualMinutes = x.ActualMinutes,
                LimitMinutes = x.LimitMinutes,
                Metadata = x.Metadata,
                ExecutionTrace = x.ExecutionTrace,
                RuleAnalysis = BuildRuleAnalysis(x)
            })
            .ToList();

        var visibleTimeline = timeline
            .Where(x => analysisRange.IntersectsVisibleRange(
                x.StartUtc,
                x.EndUtc))
            .ToList();

        var timelineEntries = includeTimeline
            ? visibleTimeline
                .Select(x => new ComplianceTimelineEntryDto
                {
                    SourceActivityId = x.SourceActivityId,
                    ActivityType = x.ActivityType,
                    StartUtc = Max(x.StartUtc, analysisRange.VisibleStartUtc),
                    EndUtc = Min(x.EndUtc, analysisRange.VisibleEndUtc),
                    DurationMinutes = (long)Math.Round((Min(x.EndUtc, analysisRange.VisibleEndUtc) - Max(x.StartUtc, analysisRange.VisibleStartUtc)).TotalMinutes)
                })
                .Where(x => x.EndUtc > x.StartUtc)
                .ToList()
            : [];

        return new CompliancePreviewResponseDto
        {
            DriverId = driverId,
            TimelineCount = visibleTimeline.Count,
            ViolationsCount = violations.Count,
            Timeline = timelineEntries,
            Violations = violations,
            DebugSummary = BuildDebugSummary(visibleTimeline)
        };
    }

    internal static ViolationRuleAnalysisDto? BuildRuleAnalysis(ComplianceViolationCandidate violation)
    {
        if (violation.ExecutionTrace is null)
        {
            return null;
        }

        var trace = violation.ExecutionTrace;
        var traceDto = MapTrace(trace);
        var analysis = new ViolationRuleAnalysisDto
        {
            RuleName = trace.RuleName,
            RuleCode = trace.RuleCode,
            ViolationDetectedAtUtc = trace.DetectedAtUtc ?? violation.PeriodEndUtc,
            AnalysisWindowStartUtc = trace.AnalysisWindowStartUtc,
            AnalysisWindowEndUtc = trace.AnalysisWindowEndUtc,
            IsEstimated = trace.IsEstimated,
            BusinessSummary = trace.Summary,
            ExecutionTrace = traceDto,
            Steps = traceDto.Steps,
            Segments = trace.Segments.Select(MapTraceSegment).ToList()
        };

        if (trace.RuleCode.Equals("CONTINUOUS_DRIVING_BREAK", StringComparison.OrdinalIgnoreCase))
        {
            analysis.DrivingLimitMinutes = GetMetadataLong(violation.Metadata, "limitMinutes") ?? violation.LimitMinutes;
            analysis.RequiredBreakMinutes = GetMetadataLong(violation.Metadata, "requiredBreakMinutes") ?? 45;
            analysis.ContinuousDrivingMinutes = GetMetadataLong(violation.Metadata, "continuousDrivingMinutes") ?? violation.ActualMinutes;
            analysis.ExceededMinutes = GetMetadataLong(violation.Metadata, "exceededMinutes") ?? Math.Max(analysis.ContinuousDrivingMinutes - analysis.DrivingLimitMinutes, 0);
        }
        else if (trace.RuleCode.Equals("DAILY_REST", StringComparison.OrdinalIgnoreCase))
        {
            analysis.RequiredRestMinutes = GetMetadataLong(violation.Metadata, "requiredRestMinutes");
            analysis.LongestRestMinutes = GetMetadataLong(violation.Metadata, "longestRestMinutes") ?? violation.ActualMinutes;
            analysis.MissingRestMinutes = GetMetadataLong(violation.Metadata, "missingRestMinutes");
        }
        else if (trace.RuleCode.Equals("DAILY_DRIVING_LIMIT", StringComparison.OrdinalIgnoreCase) ||
            trace.RuleCode.Equals("DAILY_DRIVING", StringComparison.OrdinalIgnoreCase) ||
            trace.RuleCode.Equals("WEEKLY_DRIVING_LIMIT", StringComparison.OrdinalIgnoreCase) ||
            trace.RuleCode.Equals("WEEKLY_DRIVING", StringComparison.OrdinalIgnoreCase) ||
            trace.RuleCode.Equals("BI_WEEKLY_DRIVING_LIMIT", StringComparison.OrdinalIgnoreCase) ||
            trace.RuleCode.Equals("BIWEEKLY_DRIVING_LIMIT", StringComparison.OrdinalIgnoreCase) ||
            trace.RuleCode.Equals("BIWEEKLY_DRIVING", StringComparison.OrdinalIgnoreCase))
        {
            analysis.DrivingLimitMinutes = GetMetadataLong(violation.Metadata, "limitMinutes") ?? violation.LimitMinutes;
            analysis.ContinuousDrivingMinutes = GetMetadataLong(violation.Metadata, "totalDrivingMinutes") ?? violation.ActualMinutes;
            analysis.ExceededMinutes = GetMetadataLong(violation.Metadata, "exceededMinutes") ?? Math.Max(analysis.ContinuousDrivingMinutes - analysis.DrivingLimitMinutes, 0);
        }

        return analysis;
    }

    private static RuleExecutionTraceDto MapTrace(RuleExecutionTrace trace)
    {
        return new RuleExecutionTraceDto
        {
            RuleCode = trace.RuleCode,
            RuleName = trace.RuleName,
            AnalysisWindowStartUtc = trace.AnalysisWindowStartUtc,
            AnalysisWindowEndUtc = trace.AnalysisWindowEndUtc,
            DetectedAtUtc = trace.DetectedAtUtc,
            IsEstimated = trace.IsEstimated,
            Summary = trace.Summary,
            Metrics = trace.Metrics.ToDictionary(x => x.Key, x => x.Value),
            Steps = trace.Steps.Select(x => new RuleExecutionTraceStepDto
            {
                Order = x.Order,
                TimeUtc = x.TimestampUtc,
                Description = x.Description,
                CounterMinutes = x.CounterMinutes,
                ResetsCounter = x.IsResetPoint,
                DetectsViolation = x.IsViolationPoint
            }).ToList(),
            Segments = trace.Segments.Select(x => new RuleExecutionTraceSegmentDto
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

    private static ViolationRuleAnalysisSegmentDto MapTraceSegment(RuleExecutionTraceSegment segment)
    {
        return new ViolationRuleAnalysisSegmentDto
        {
            StartUtc = segment.StartUtc,
            EndUtc = segment.EndUtc,
            ActivityType = segment.ActivityType,
            DurationMinutes = segment.DurationMinutes,
            IncreasesDrivingCounter = segment.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase),
            ResetsCounter = segment.IsResetPoint,
            DrivingCounterAfterSegment = segment.DrivingMinutesAfterSegment,
            CountsAsRest = segment.RestCandidateMinutes.HasValue,
            RestCandidateMinutes = segment.RestCandidateMinutes
        };
    }

    private static long? GetMetadataLong(IReadOnlyDictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            double doubleValue => (long)Math.Round(doubleValue),
            decimal decimalValue => (long)Math.Round(decimalValue),
            string text when long.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private async Task<IReadOnlyList<ComplianceCountryEntry>> BuildCountryEntriesForDriverAsync(
        Guid companyId,
        Guid driverId,
        DateTime queryStartUtc,
        DateTime queryEndUtc,
        CancellationToken cancellationToken)
    {
        return await _dbContext.CountryEntries
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == companyId &&
                x.DddFile.DriverId == driverId &&
                x.EntryTimeUtc >= queryStartUtc &&
                x.EntryTimeUtc <= queryEndUtc)
            .OrderBy(x => x.EntryTimeUtc)
            .Select(x => new ComplianceCountryEntry
            {
                SourceCountryEntryId = x.Id,
                DriverId = driverId,
                DddFileId = x.DddFileId,
                CountryCode = x.CountryCode,
                EntryType = x.EntryType,
                EntryTimeUtc = x.EntryTimeUtc
            })
            .ToListAsync(cancellationToken);
    }

    private static DateTime Min(DateTime left, DateTime right) =>
        left <= right ? left : right;

    private static DateTime Max(DateTime left, DateTime right) =>
        left >= right ? left : right;

    private ComplianceDebugSummaryDto BuildDebugSummary(
        IReadOnlyList<TimelineActivity> timeline)
    {
        var drivingActivities = WeeklyDrivingTimelineHelper.GetMergedDrivingTimeline(timeline)
            .ToList();

        return new ComplianceDebugSummaryDto
        {
            DrivingMinutesByDay = drivingActivities
                .GroupBy(x => x.StartUtc.Date)
                .OrderBy(x => x.Key)
                .ToDictionary(
                    x => x.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    x => SumMinutes(x)),
            RestMinutesByDay = timeline
                .Where(x => x.ActivityType.Equals(ActivityTypeNormalizer.Rest, StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.StartUtc.Date)
                .OrderBy(x => x.Key)
                .ToDictionary(
                    x => x.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    x => SumMinutes(x)),
            WeeklyDrivingMinutes = drivingActivities
                .GroupBy(x => GetIsoWeekStart(x.StartUtc))
                .OrderBy(x => x.Key)
                .ToDictionary(
                    x => x.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    x => SumMinutes(x)),
            BiWeeklyDrivingMinutes = BuildBiWeeklyDrivingMinutes(drivingActivities),
            RegisteredRuleCodes = _rules
                .Select(x => x.Code)
                .Concat(_countryEntryRules.Select(x => x.Code))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList()
        };
    }

    private static Dictionary<string, long> BuildBiWeeklyDrivingMinutes(
        IReadOnlyList<TimelineActivity> drivingActivities)
    {
        var weekly = drivingActivities
            .GroupBy(x => GetIsoWeekStart(x.StartUtc))
            .Select(x => new
            {
                WeekStart = x.Key,
                Minutes = SumMinutes(x)
            })
            .OrderBy(x => x.WeekStart)
            .ToList();
        var result = new Dictionary<string, long>();

        for (var index = 1; index < weekly.Count; index++)
        {
            var previous = weekly[index - 1];
            var current = weekly[index];

            if (current.WeekStart != previous.WeekStart.AddDays(7))
            {
                continue;
            }

            var key = $"{previous.WeekStart:yyyy-MM-dd}_{current.WeekStart:yyyy-MM-dd}";
            result[key] = previous.Minutes + current.Minutes;
        }

        return result;
    }

    private static DateTime GetIsoWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);

        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }

    private static long SumMinutes(IEnumerable<TimelineActivity> activities) =>
        activities.Sum(x => (long)Math.Round(x.Duration.TotalMinutes));
}
