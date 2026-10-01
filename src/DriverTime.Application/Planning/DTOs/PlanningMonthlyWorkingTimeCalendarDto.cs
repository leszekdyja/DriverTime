namespace DriverTime.Application.Planning.DTOs;

public class PlanningMonthlyWorkingTimeCalendarDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int WeekdayCount { get; set; }

    public int HolidayReductionDays { get; set; }

    public int WorkingDays { get; set; }

    public int TargetWorkMinutes { get; set; }

    public List<PlanningPublicHolidayDto> Holidays { get; set; } = new();

    public List<DateOnly> StandardWorkingDays { get; set; } = new();
}

public class PlanningPublicHolidayDto
{
    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;
}
