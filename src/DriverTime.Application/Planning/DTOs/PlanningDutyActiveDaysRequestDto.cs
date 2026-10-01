namespace DriverTime.Application.Planning.DTOs;

public class PlanningDutyActiveDaysRequestDto
{
    public int? ActiveDaysMask { get; set; }

    public bool IncludeHolidays { get; set; }
}
