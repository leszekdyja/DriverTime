namespace DriverTime.Application.Planning.DTOs;

public class PlanningAutoGenerateRequestDto
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public List<Guid> DriverIds { get; set; } = new();

    public int? MinDailyRestMinutes { get; set; }

    public int? MaxConsecutiveWorkDays { get; set; }

    public int? MaxWeeklyWorkMinutes { get; set; }

    public int? TargetMonthlyWorkMinutes { get; set; }

    public bool? CalculateMonthlyTargetFromCalendar { get; set; }

    public bool? EnforceMonthlyTargetMaximum { get; set; }

    public bool? EnforceWeeklyMaximum { get; set; }

    public bool? IncludeManualAssignmentsInWorkload { get; set; }

    public bool? IncludeAssignmentsOutsideGeneratedRangeForRestChecks { get; set; }

    public int? MinWeeklyRestMinutes { get; set; }

    public int? RegularWeeklyRestMinutes { get; set; }

    public int? PreferredWeeklyRestMinutes { get; set; }

    public List<PlanningAssignmentRuleDto> AssignmentRules { get; set; } = new();
}
