namespace DriverTime.Application.Planning;

public enum PlanningCandidateRejectionReason
{
    DriverNotFound,
    DutyMissingStartOrEndTime,
    AlreadyAssignedOnDate,
    ManualAssignmentOnDate,
    Vacation,
    SickLeave,
    DayOff,
    Unavailable,
    OverlappingAssignment,
    InsufficientDailyRestBefore,
    InsufficientDailyRestAfter,
    TooManyConsecutiveWorkDays,
    WeeklyWorkMinutesExceeded,
    MonthlyWorkMinutesExceeded,
    InsufficientWeeklyRest,
    DriverDutyForbidden
}
