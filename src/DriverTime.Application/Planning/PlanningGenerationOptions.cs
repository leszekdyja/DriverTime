namespace DriverTime.Application.Planning;

public record PlanningGenerationOptions
{
    public const int DefaultMinDailyRestMinutes = 540;
    public const int DefaultMaxConsecutiveWorkDays = 6;
    public const int DefaultMaxWeeklyWorkMinutes = 3600;
    public const int DefaultMinWeeklyRestMinutes = 1440;
    public const int DefaultRegularWeeklyRestMinutes = 2700;
    public const int DefaultPreferredWeeklyRestMinutes = 2100;

    public int MinDailyRestMinutes { get; init; } = DefaultMinDailyRestMinutes;

    public int MaxConsecutiveWorkDays { get; init; } = DefaultMaxConsecutiveWorkDays;

    public int MaxWeeklyWorkMinutes { get; init; } = DefaultMaxWeeklyWorkMinutes;

    public int? TargetMonthlyWorkMinutes { get; init; }

    public PlanningMonthlyTargetWorkMinutesSource TargetMonthlyWorkMinutesSource { get; init; } = PlanningMonthlyTargetWorkMinutesSource.None;

    public bool CalculateMonthlyTargetFromCalendar { get; init; } = true;

    public bool EnforceMonthlyTargetMaximum { get; init; }

    public bool EnforceWeeklyMaximum { get; init; } = true;

    public bool IncludeManualAssignmentsInWorkload { get; init; } = true;

    public bool IncludeAssignmentsOutsideGeneratedRangeForRestChecks { get; init; } = true;

    public int MinWeeklyRestMinutes { get; init; } = DefaultMinWeeklyRestMinutes;

    public int RegularWeeklyRestMinutes { get; init; } = DefaultRegularWeeklyRestMinutes;

    public int? PreferredWeeklyRestMinutes { get; init; } = DefaultPreferredWeeklyRestMinutes;

    public IReadOnlyCollection<PlanningAssignmentRule> AssignmentRules { get; init; } = Array.Empty<PlanningAssignmentRule>();
}
