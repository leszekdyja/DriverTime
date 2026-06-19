using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class DrivingLimitRuleTests
{
    private readonly DailyDrivingLimitRule _dailyRule = new(NullLogger<DailyDrivingLimitRule>.Instance);
    private readonly WeeklyDrivingLimitRule _weeklyRule = new(NullLogger<WeeklyDrivingLimitRule>.Instance);
    private readonly BiWeeklyDrivingLimitRule _biWeeklyRule = new(NullLogger<BiWeeklyDrivingLimitRule>.Instance);
    private readonly ContinuousDrivingBreakRule _continuousRule = new(NullLogger<ContinuousDrivingBreakRule>.Instance);

    [TestMethod]
    public void DailyDrivingLimit_SplitsActivityCrossingMidnight()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T22:00:00Z", "2026-06-09T02:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-09T08:00:00Z", "2026-06-09T15:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_MergesOverlappingDrivingRecords()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T14:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:00:00Z", "2026-06-08T16:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_IgnoresDuplicatedDrivingPeriodFromSecondImport()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T06:00:00Z", "2026-06-08T15:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T06:00:00Z", "2026-06-08T15:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_DoesNotCountDrivingCoveredByNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildCoveredLongDrivingTimeline(driverId);

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

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
    public void WeeklyDrivingLimit_DoesNotDoubleCountDuplicatedWeek()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T16:00:00Z")
        };

        var result = _weeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void WeeklyDrivingLimit_DoesNotCountDrivingCoveredByNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildCoveredLongDrivingTimeline(driverId);

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

    [TestMethod]
    public void BiWeeklyDrivingLimit_DoesNotDoubleCountDuplicatedTwoWeekPeriod()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T21:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-09T21:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-15T00:00:00Z", "2026-06-16T21:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-15T00:00:00Z", "2026-06-16T21:00:00Z")
        };

        var result = _biWeeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void BiWeeklyDrivingLimit_DoesNotCountDrivingCoveredByNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildCoveredLongDrivingTimeline(driverId);

        var result = _biWeeklyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_DoesNotCountDrivingCoveredByNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildCoveredLongDrivingTimeline(driverId);

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    private static TimelineActivity[] BuildCoveredLongDrivingTimeline(Guid driverId)
    {
        return
        [
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T00:00:00Z", "2026-06-08T22:56:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T00:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T10:00:00Z", "2026-06-08T10:07:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:07:00Z", "2026-06-08T10:39:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:39:00Z", "2026-06-09T00:00:00Z")
        ];
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
