using DriverTime.Application.Planning;
using DriverTime.Domain.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningChristmasDutiesTests
{
    [TestMethod]
    public void ChristmasAlwaysUsesSundayMaskIncludingWhenHolidayFlagIsEnabled()
    {
        for (var year = 2026; year < 2040; year++)
        foreach (var day in new[] {25, 26})
        foreach (var mask in new[] {0, PlanningDutyDayAvailability.WeekdaysMask,
                     PlanningDutyDayAvailability.Saturday, PlanningDutyDayAvailability.Sunday,
                     PlanningDutyDayAvailability.AllWeekMask})
        foreach (var includeHolidays in new[] {false, true})
        {
            var duty = new PlanningDuty {ActiveDaysMask = mask, IncludeHolidays = includeHolidays};
            var expected = (mask & PlanningDutyDayAvailability.Sunday) != 0;
            Assert.AreEqual(expected, PlanningDutyDayAvailability.IsActiveOn(duty,
                new DateOnly(year, 12, day), isHoliday: true));
            Assert.AreEqual(expected, PlanningDutyDayAvailability.IsActiveOn(duty,
                new DateOnly(year, 12, day)));
        }
    }

    [TestMethod]
    public void OtherHolidaysKeepTheirExistingAvailability()
    {
        var duty = new PlanningDuty {ActiveDaysMask = 0, IncludeHolidays = true};
        Assert.IsTrue(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 11, 11), true));
        Assert.IsTrue(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 12, 24), true));
        Assert.IsFalse(PlanningDutyDayAvailability.IsActiveOn(duty, new DateOnly(2026, 12, 28)));
    }
}
