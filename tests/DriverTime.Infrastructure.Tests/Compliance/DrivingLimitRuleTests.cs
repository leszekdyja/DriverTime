using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class DrivingLimitRuleTests
{
    private readonly WeeklyDrivingLimitRule _weeklyRule = new(NullLogger<WeeklyDrivingLimitRule>.Instance);
    private readonly BiWeeklyDrivingLimitRule _biWeeklyRule = new(NullLogger<BiWeeklyDrivingLimitRule>.Instance);

    [TestMethod]
    public void WeeklyDrivingLimit_WithExactlyFiftySixHours_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-10T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-10T08:00:00Z", "2026-06-12T08:00:00Z")
        };

        var result = _weeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void WeeklyDrivingLimit_WithMoreThanFiftySixHoursDriving_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-10T09:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-10T09:00:00Z", "2026-06-12T09:00:00Z")
        };

        var result = _weeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(57 * 60, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void WeeklyDrivingLimit_IgnoresNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-09T00:00:00Z", "2026-06-11T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-11T00:00:00Z", "2026-06-13T00:00:00Z")
        };

        var result = _weeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void WeeklyDrivingLimit_SplitsDrivingActivityAtIsoWeekBoundary()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-14T20:00:00Z", "2026-06-15T04:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-15T04:00:00Z", "2026-06-17T08:00:00Z")
        };

        var result = _weeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void BiWeeklyDrivingLimit_WithExactlyNinetyHours_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T21:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-15T00:00:00Z", "2026-06-16T21:00:00Z")
        };

        var result = _biWeeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void BiWeeklyDrivingLimit_WithMoreThanNinetyHoursInConsecutiveWeeks_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-10T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-15T00:00:00Z", "2026-06-16T19:00:00Z")
        };

        var result = _biWeeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(91 * 60, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void BiWeeklyDrivingLimit_IgnoresNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-09T00:00:00Z", "2026-06-12T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-15T00:00:00Z", "2026-06-18T00:00:00Z")
        };

        var result = _biWeeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void BiWeeklyDrivingLimit_SplitsDrivingActivityAtIsoWeekBoundary()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-14T12:00:00Z", "2026-06-15T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-15T12:00:00Z", "2026-06-17T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-22T00:00:00Z", "2026-06-23T18:00:00Z")
        };

        var result = _biWeeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    private static TimelineActivity Activity(
        Guid driverId,
        string activityType,
        string startUtc,
        string endUtc)
    {
        return new TimelineActivity
        {
            SourceActivityId = Guid.NewGuid(),
            DriverId = driverId,
            ActivityType = activityType,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }
}
