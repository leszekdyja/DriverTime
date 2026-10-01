namespace DriverTime.Application.Compliance.DTOs;

public class CompliancePreviewResponseDto
{
    public Guid DriverId { get; set; }

    public int TimelineCount { get; set; }

    public int ViolationsCount { get; set; }

    public List<ComplianceTimelineEntryDto> Timeline { get; set; } = new();

    public List<ComplianceViolationPreviewDto> Violations { get; set; } = new();

    public ComplianceDebugSummaryDto DebugSummary { get; set; } = new();
}
