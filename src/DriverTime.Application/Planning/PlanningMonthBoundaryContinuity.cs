using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public static class PlanningMonthBoundaryContinuity
{
    public static HashSet<Guid> GetContinuingNightDriverIds(DateOnly day, IEnumerable<PlanningAssignment> assignments)
    {
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        if (monthStart.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            || day.DayNumber - monthStart.DayNumber > (int)DayOfWeek.Friday - (int)monthStart.DayOfWeek)
            return new HashSet<Guid>();
        return assignments.Where(x => x.Date == monthStart.AddDays(-1)
                && PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.NightDuty)
            .Select(x => x.DriverId).ToHashSet();
    }

    public static decimal GetScore(Guid driverId, PlanningDuty duty, DateOnly day,
        IEnumerable<PlanningAssignment> assignments)
    {
        var monthStart = new DateOnly(day.Year, day.Month, 1);
        if (monthStart.DayOfWeek is not (DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday)
            || day.DayNumber - monthStart.DayNumber > (int)DayOfWeek.Friday - (int)monthStart.DayOfWeek
            || PlanningEntryClassifier.Classify(duty).Kind != PlanningEntryKind.Duty)
            return 0;

        var holidays = new PolishPublicHolidayProvider().GetHolidays(day.Year);
        if (holidays.Any(x => x.Date == day)) return 0;
        var monday = monthStart.AddDays(1 - (int)monthStart.DayOfWeek);
        var last = assignments
            .Where(x => x.DriverId == driverId && x.Date >= monday && x.Date < monthStart
                && !holidays.Any(h => h.Date == x.Date)
                && PlanningEntryClassifier.Classify(x).Kind == PlanningEntryKind.Duty)
            .OrderByDescending(x => x.Date).ThenBy(x => x.Id)
            .FirstOrDefault();
        return last?.PlanningDutyId == duty.Id ? -100000m : 0m;
    }
}
