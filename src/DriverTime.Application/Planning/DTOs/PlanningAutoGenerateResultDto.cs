namespace DriverTime.Application.Planning.DTOs;

public class PlanningAutoGenerateResultDto
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public bool IsPreview { get; set; }

    public List<PlanningAssignmentListItemDto> ProposedAssignments { get; set; } = new();

    public int GeneratedCount { get; set; }

    public int ConflictCount { get; set; }

    public int CandidateRejectionCount { get; set; }

    public int TimeConflictRejectionCount { get; set; }

    public int DailyRestRejectionCount { get; set; }

    public int WeeklyRestRejectionCount { get; set; }

    public int ManualAssignmentsPreserved { get; set; }

    public int NightDutyGeneratedCount { get; set; }

    public int RescueResolvedDutyCount { get; set; }

    public int DirectSwapCount { get; set; }

    public int ChainSwapCount { get; set; }

    public int RemovedWeeklyDayOffCount { get; set; }

    public int RemovedDayOffCount { get; set; }

    public int RemovedReserveFirstShiftCount { get; set; }

    public int RemovedReserveSecondShiftCount { get; set; }

    public int ReserveGeneratedCount { get; set; }

    public int DayOffGeneratedCount { get; set; }

    public int ForbiddenCandidateRejectionCount { get; set; }

    public int PreferredAssignmentCount { get; set; }

    public int ConstraintBlockedUnassignedCount { get; set; }

    public int VehicleDataWarningCount { get; set; }

    public int UnassignedCount { get; set; }

    public List<PlanningUnassignedDutyDto> UnassignedDuties { get; set; } = new();

    public List<PlanningDriverGenerationSummaryDto> DriverSummaries { get; set; } = new();

    public List<PlanningMonthlyWorkingTimeCalendarDto> MonthlyCalendars { get; set; } = new();

    public List<string> Warnings { get; set; } = new();

    public List<string> Messages { get; set; } = new();

    public List<PlanningGenerationTimingDto> Timings { get; set; } = new();
}


public class PlanningGenerationTimingDto
{
    public string Stage { get; set; } = string.Empty;

    public long ElapsedMilliseconds { get; set; }
}

