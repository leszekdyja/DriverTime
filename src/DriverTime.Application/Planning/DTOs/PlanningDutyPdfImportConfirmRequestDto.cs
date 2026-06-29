namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportConfirmRequestDto
{
    public string? SourceFileName { get; set; }

    public List<PlanningDutyPdfImportConfirmItemDto> Duties { get; set; } = new();
}
