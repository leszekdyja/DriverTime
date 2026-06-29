namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportPreviewDto
{
    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int DetectedDutyCount { get; set; }

    public List<string> Warnings { get; set; } = new();

    public List<PlanningDutyPdfImportPreviewItemDto> Duties { get; set; } = new();
}
