using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningNightHolidayCoverageTests
{
    [TestMethod]
    public void HolidayBeforeWorkingDayRequiresTwoNightDrivers()
    {
        Assert.AreEqual(2, PlanningTechnicalAssignmentPlanner.NightDutyRequiredCount(new DateOnly(2026, 11, 11)));
    }

    [TestMethod]
    public void HolidayBeforeHolidayOrWeekendRequiresOneNightDriver()
    {
        Assert.AreEqual(1, PlanningTechnicalAssignmentPlanner.NightDutyRequiredCount(new DateOnly(2025, 12, 25)));
        Assert.AreEqual(1, PlanningTechnicalAssignmentPlanner.NightDutyRequiredCount(new DateOnly(2026, 12, 25)));
    }
}
