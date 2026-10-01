namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyPdfImportConfirmResultItemDto
{
    public string? DutyNumber { get; set; }

    public string? Line { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
