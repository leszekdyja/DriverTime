using DriverTime.Application.Planning;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningHolidayWorkingHoursTests
{
    private static PlanningWorkingTimeCalendarService Calendar() => new(new PolishPublicHolidayProvider());

    [TestMethod]
    public void December2026Has160HoursAndThreeReducingHolidays()
    {
        var result = Calendar().BuildMonthlyCalendar(2026, 12);
        Assert.AreEqual(160 * 60, result.TargetWorkMinutes);
        Assert.AreEqual(23, result.WeekdayCount);
        Assert.AreEqual(3, result.PublicHolidayReductionDays);
        foreach (var day in new[] {24, 25, 26})
        {
            Assert.IsTrue(result.Holidays.Any(x => x.Date == new DateOnly(2026, 12, day)));
            Assert.IsFalse(Calendar().IsStandardWorkingDay(new DateOnly(2026, 12, day)));
        }
    }

    [TestMethod]
    public void ChristmasEveIsHolidayOnlyFrom2025AndSundayDoesNotReduceNorm()
    {
        var provider = new PolishPublicHolidayProvider();
        Assert.IsFalse(provider.GetHolidays(2024).Any(x => x.Date == new DateOnly(2024, 12, 24)));
        Assert.IsTrue(provider.GetHolidays(2025).Any(x => x.Date == new DateOnly(2025, 12, 24)));
        foreach (var (year, hours) in new[] {(2024, 160), (2025, 160), (2026, 160), (2027, 168), (2028, 152)})
            Assert.AreEqual(hours * 60, Calendar().BuildMonthlyCalendar(year, 12).TargetWorkMinutes, $"Year {year}");
    }

    [TestMethod]
    public void AllMonthlyNormsIn2026AreCorrect()
    {
        var expected = new[] {160, 160, 176, 168, 160, 168, 184, 160, 176, 176, 160, 160};
        for (var month = 1; month <= 12; month++)
            Assert.AreEqual(expected[month - 1] * 60, Calendar().BuildMonthlyCalendar(2026, month).TargetWorkMinutes, $"Month {month}");
    }
}
