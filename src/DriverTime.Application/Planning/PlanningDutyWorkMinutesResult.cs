namespace DriverTime.Application.Planning;

public readonly record struct PlanningDutyWorkMinutesResult(
    int WorkMinutes,
    PlanningDutyWorkMinutesSource Source,
    string? Warning);
