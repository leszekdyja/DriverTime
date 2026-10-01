namespace DriverTime.Application.Planning;

public class PlanningWorkingTimeCalendarService
{
    private readonly IPolishPublicHolidayProvider _holidayProvider;

    public PlanningWorkingTimeCalendarService(IPolishPublicHolidayProvider holidayProvider)
    {
        _holidayProvider = holidayProvider;
    }

    public PlanningMonthlyWorkingTimeCalendar BuildMonthlyCalendar(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var holidays = _holidayProvider.GetHolidays(year)
            .Where(x => x.Date.Month == month)
            .OrderBy(x => x.Date)
            .ToList();
        var holidayDates = holidays.Select(x => x.Date).ToHashSet();
        var standardWorkingDays = EachDate(first, last)
            .Where(IsWeekday)
            .Where(x => !holidayDates.Contains(x))
            .ToList();
        var weekdayCount = EachDate(first, last).Count(IsWeekday);
        var reductionDays = holidays.Count(x => x.Date.DayOfWeek != DayOfWeek.Sunday);

        return new PlanningMonthlyWorkingTimeCalendar
        {
            Year = year,
            Month = month,
            WeekdayCount = weekdayCount,
            PublicHolidayReductionDays = reductionDays,
            WorkingDays = standardWorkingDays.Count,
            TargetWorkMinutes = Math.Max(0, weekdayCount - reductionDays) * 480,
            Holidays = holidays,
            StandardWorkingDays = standardWorkingDays
        };
    }

    public bool IsStandardWorkingDay(DateOnly date)
    {
        if (!IsWeekday(date))
        {
            return false;
        }

        return !_holidayProvider.GetHolidays(date.Year).Any(x => x.Date == date);
    }

    private static bool IsWeekday(DateOnly date) =>
        date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

    private static IEnumerable<DateOnly> EachDate(DateOnly dateFrom, DateOnly dateTo)
    {
        for (var date = dateFrom; date <= dateTo; date = date.AddDays(1))
        {
            yield return date;
        }
    }
}
