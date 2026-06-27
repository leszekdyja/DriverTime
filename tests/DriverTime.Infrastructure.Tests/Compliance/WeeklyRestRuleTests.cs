using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class WeeklyRestRuleTests
{
    private readonly RegularWeeklyRestRule _regularRule = new(NullLogger<RegularWeeklyRestRule>.Instance);
    private readonly ReducedWeeklyRestRule _reducedRule = new(NullLogger<ReducedWeeklyRestRule>.Instance);

    [TestMethod]
    public void RegularWeeklyRest_WithFortyFiveHourRest_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-10T13:00:00Z", "2026-06-10T16:00:00Z")
        };

        var result = _regularRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ReducedWeeklyRest_WithTwentyFourHourRest_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-09T16:00:00Z", "2026-06-09T20:00:00Z")
        };

        var result = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ThirtyHourRest_IsReducedWeeklyRestButNotRegularWeeklyRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-09T22:00:00Z", "2026-06-10T02:00:00Z")
        };

        var reducedResult = _reducedRule.Evaluate(driverId, timeline);
        var regularResult = _regularRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, reducedResult.Violations.Count);
        Assert.AreEqual(0, regularResult.Violations.Count);
    }

    [TestMethod]
    public void ReducedWeeklyRest_WithLessThanTwentyFourHours_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-09T15:30:00Z", "2026-06-09T20:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-09T20:00:00Z", "2026-06-15T00:00:00Z")
        };

        var result = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(23 * 60 + 30, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void WeeklyRest_WithActivityCrossingMidnight_UsesRealUtcTimeline()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T22:00:00Z", "2026-06-09T02:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-11T00:00:00Z", "2026-06-11T04:00:00Z")
        };

        var regularResult = _regularRule.Evaluate(driverId, timeline);
        var reducedResult = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, regularResult.Violations.Count);
        Assert.AreEqual(0, reducedResult.Violations.Count);
    }

    [TestMethod]
    public void WeeklyRest_StartingInOneWeekAndEndingInNextWeek_IsCountedAsContinuousRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-13T08:00:00Z", "2026-06-13T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-16T09:00:00Z", "2026-06-16T13:00:00Z")
        };

        var regularResult = _regularRule.Evaluate(driverId, timeline);
        var reducedResult = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, regularResult.Violations.Count);
        Assert.AreEqual(0, reducedResult.Violations.Count);
    }

    [TestMethod]
    public void WeeklyRest_RealGapBetweenActivities_IsCountedAsRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T06:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-09T10:00:00Z", "2026-06-09T14:00:00Z")
        };

        var result = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void WeeklyRest_AvailabilitySegment_IsNotCountedAsRestCompatibleTime()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T16:00:00Z", "2026-06-10T13:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-10T13:00:00Z", "2026-06-10T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-10T16:00:00Z", "2026-06-15T00:00:00Z")
        };

        var regularResult = _regularRule.Evaluate(driverId, timeline);
        var reducedResult = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, regularResult.Violations.Count);
        Assert.AreEqual(1, reducedResult.Violations.Count);
        Assert.AreEqual(0, reducedResult.Violations[0].ActualMinutes);
        Assert.AreEqual(24 * 60, reducedResult.Violations[0].LimitMinutes);
        Assert.AreEqual(24L * 60, reducedResult.Violations[0].Metadata["missingRestMinutes"]);
    }

    [TestMethod]
    public void ReducedWeeklyRest_WithFortyFourHourRest_ReturnsNoMinimumViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-10T12:00:00Z", "2026-06-10T16:00:00Z")
        };

        var reducedResult = _reducedRule.Evaluate(driverId, timeline);
        var regularResult = _regularRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, reducedResult.Violations.Count);
        Assert.AreEqual(0, regularResult.Violations.Count);
    }

    [TestMethod]
    public void ReducedWeeklyRest_WithIncompleteIsoWeekAfterReducedRest_DoesNotReturnViolationForOpenWeek()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-12T10:00:00Z", "2026-06-12T14:12:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-12T14:12:00Z", "2026-06-14T06:54:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-14T06:54:00Z", "2026-06-14T08:26:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-15T08:00:00Z", "2026-06-15T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T10:00:00Z")
        };

        var result = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ReducedWeeklyRest_WithClosedIsoWeekWithoutTwentyFourHourRest_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T16:00:00Z", "2026-06-15T00:00:00Z")
        };

        var result = _reducedRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("REDUCED_WEEKLY_REST", result.Violations[0].Code);
        Assert.AreEqual(DateTime.Parse("2026-06-08T00:00:00Z").ToUniversalTime(), result.Violations[0].PeriodStartUtc);
        Assert.AreEqual(DateTime.Parse("2026-06-15T00:00:00Z").ToUniversalTime(), result.Violations[0].PeriodEndUtc);
    }

    [TestMethod]
    public void WeeklyRest_WithSeveralConsecutiveWeeks_UsesEachContinuousWeeklyRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-10T13:00:00Z", "2026-06-10T17:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-15T08:00:00Z", "2026-06-15T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-16T16:00:00Z", "2026-06-16T20:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-22T08:00:00Z", "2026-06-22T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-24T13:00:00Z", "2026-06-24T17:00:00Z")
        };

        var reducedResult = _reducedRule.Evaluate(driverId, timeline);
        var regularResult = _regularRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, reducedResult.Violations.Count);
        Assert.AreEqual(0, regularResult.Violations.Count);
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
