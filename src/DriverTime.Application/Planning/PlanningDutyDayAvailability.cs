using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public static class PlanningDutyDayAvailability
{
    public const int Monday = 1 << 0;
    public const int Tuesday = 1 << 1;
    public const int Wednesday = 1 << 2;
    public const int Thursday = 1 << 3;
    public const int Friday = 1 << 4;
    public const int Saturday = 1 << 5;
    public const int Sunday = 1 << 6;

    public const int WeekdaysMask = Monday | Tuesday | Wednesday | Thursday | Friday;
    public const int WeekendMask = Saturday | Sunday;
    public const int AllWeekMask = WeekdaysMask | WeekendMask;

    public static int NormalizeMask(int? mask) => mask.HasValue
        ? mask.Value & AllWeekMask
        : WeekdaysMask;

    public static bool IsActiveOn(PlanningDuty duty, DateOnly date, bool isHoliday = false)
    {
        if (isHoliday && duty.IncludeHolidays)
        {
            return true;
        }

        var dayMask = date.DayOfWeek switch
        {
            DayOfWeek.Monday => Monday,
            DayOfWeek.Tuesday => Tuesday,
            DayOfWeek.Wednesday => Wednesday,
            DayOfWeek.Thursday => Thursday,
            DayOfWeek.Friday => Friday,
            DayOfWeek.Saturday => Saturday,
            DayOfWeek.Sunday => Sunday,
            _ => 0
        };

        return (NormalizeMask(duty.ActiveDaysMask) & dayMask) != 0;
    }
}

