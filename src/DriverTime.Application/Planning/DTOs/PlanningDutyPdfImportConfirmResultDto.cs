namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportConfirmResultDto
{
    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int UnchangedCount { get; set; }

    public int SkippedCount { get; set; }

    public List<string> Errors { get; set; } = new();

    public List<PlanningDutyPdfImportConfirmResultItemDto> Items { get; set; } = new();
}
