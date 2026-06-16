using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace DriverTime.Infrastructure.Compliance;

public class ComplianceEngineService : IComplianceEngineService
{
    private readonly ITimelineBuilderService _timelineBuilder;
    private readonly IEnumerable<IComplianceRule> _rules;
    private readonly ILogger<ComplianceEngineService> _logger;

    public ComplianceEngineService(
        ITimelineBuilderService timelineBuilder,
        IEnumerable<IComplianceRule> rules,
        ILogger<ComplianceEngineService> logger)
    {
        _timelineBuilder = timelineBuilder;
        _rules = rules;
        _logger = logger;
    }

    public async Task<CompliancePreviewResponseDto?> PreviewForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var timeline = await _timelineBuilder.BuildForDriverAsync(
            companyId,
            driverId,
            cancellationToken);

        if (timeline is null)
        {
            return null;
        }

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

        var violations = violationCandidates
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
                Metadata = x.Metadata
            })
            .ToList();

        var timelineEntries = timeline
            .Select(x => new ComplianceTimelineEntryDto
            {
                SourceActivityId = x.SourceActivityId,
                ActivityType = x.ActivityType,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                DurationMinutes = (long)Math.Round(x.Duration.TotalMinutes)
            })
            .ToList();

        return new CompliancePreviewResponseDto
        {
            DriverId = driverId,
            TimelineCount = timelineEntries.Count,
            ViolationsCount = violations.Count,
            Timeline = timelineEntries,
            Violations = violations,
            DebugSummary = BuildDebugSummary(timeline)
        };
    }

    private ComplianceDebugSummaryDto BuildDebugSummary(
        IReadOnlyList<TimelineActivity> timeline)
    {
        var drivingActivities = timeline
            .Where(x => x.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase))
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
