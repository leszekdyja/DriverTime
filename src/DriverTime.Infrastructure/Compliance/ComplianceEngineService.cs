using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;

namespace DriverTime.Infrastructure.Compliance;

public class ComplianceEngineService : IComplianceEngineService
{
    private readonly ITimelineBuilderService _timelineBuilder;
    private readonly IEnumerable<IComplianceRule> _rules;

    public ComplianceEngineService(
        ITimelineBuilderService timelineBuilder,
        IEnumerable<IComplianceRule> rules)
    {
        _timelineBuilder = timelineBuilder;
        _rules = rules;
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

        var violations = _rules
            .SelectMany(rule => rule.Evaluate(driverId, timeline).Violations)
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
            Violations = violations
        };
    }
}
