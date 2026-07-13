namespace DriverTime.Application.Planning;

public class PlanningMonthlyWorkingTimeCalendar
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int WeekdayCount { get; init; }

    public int PublicHolidayReductionDays { get; init; }

    public int WorkingDays { get; init; }

    public int TargetWorkMinutes { get; init; }

    public List<PolishPublicHoliday> Holidays { get; init; } = new();

    public List<DateOnly> StandardWorkingDays { get; init; } = new();
}
