using DriverTime.Application.Planning;

namespace DriverTime.Application.Planning.DTOs;

public class PlanningCandidateScoreBreakdownDto
{
    public decimal AssignmentCountScore { get; set; }

    public decimal WorkMinutesScore { get; set; }

    public decimal MonthlyDeficitScore { get; set; }

    public decimal CalendarTargetDeficitScore { get; set; }

    public decimal WeeklyLoadScore { get; set; }

    public decimal ConsecutiveDaysScore { get; set; }

    public decimal WeeklyRestScore { get; set; }

    public decimal ReducedWeeklyRestPenalty { get; set; }

    public decimal PreferenceScore { get; set; }

    public decimal TotalScore { get; set; }
}
