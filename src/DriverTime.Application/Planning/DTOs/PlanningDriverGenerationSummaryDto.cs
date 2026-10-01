namespace DriverTime.Application.Planning.DTOs;

public class PlanningDriverGenerationSummaryDto
{
    public Guid DriverId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public int ExistingManualAssignments { get; set; }

    public int GeneratedAssignments { get; set; }

    public int TotalAssignments { get; set; }

    public int WorkMinutesBefore { get; set; }

    public int GeneratedWorkMinutes { get; set; }

    public int WorkMinutesAfter { get; set; }

    public int RealWorkMinutesBefore { get; set; }

    public int CreditedAbsenceMinutesBefore { get; set; }

    public int RealWorkMinutesGenerated { get; set; }

    public int CreditedAbsenceMinutesGenerated { get; set; }

    public int RealWorkMinutesAfter { get; set; }

    public int CreditedAbsenceMinutesAfter { get; set; }

    public int MonthlyNormMinutesAfter { get; set; }

    public int? TargetMonthlyWorkMinutes { get; set; }

    public string TargetMonthlyWorkMinutesSource { get; set; } = "None";

    public int? CalendarTargetWorkMinutes { get; set; }

    public int? MonthlyDeficitAfter { get; set; }

    public int MaxWeeklyWorkMinutesObserved { get; set; }

    public int MaxConsecutiveWorkDaysObserved { get; set; }

    public int ReducedWeeklyRestCount { get; set; }

    public int RegularWeeklyRestCount { get; set; }

    public int InsufficientWeeklyRestCount { get; set; }

    public List<string> WeeklyRestWarnings { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}
