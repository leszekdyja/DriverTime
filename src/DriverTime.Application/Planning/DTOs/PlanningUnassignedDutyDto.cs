namespace DriverTime.Application.Planning.DTOs;

public class PlanningUnassignedDutyDto
{
    public Guid DutyId { get; set; }

    public string DutyNumber { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public DateTime? StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public List<PlanningCandidateEvaluationDto> CandidateEvaluations { get; set; } = new();

    public string Summary { get; set; } = string.Empty;
}
