using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class DailyRestViolationRuleTests
{
    private readonly DailyRestViolationRule _rule = new(NullLogger<DailyRestViolationRule>.Instance);

    [TestMethod]
    public void Evaluate_WithSeventeenHourRestBetweenActivities_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T09:00:00Z", "2026-06-18T12:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithElevenHourRestBetweenActivities_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T03:00:00Z", "2026-06-18T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithNineHourRestBetweenActivities_ReturnsReducedRestWarning()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T01:00:00Z", "2026-06-18T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MEDIUM", result.Violations[0].Severity);
        Assert.AreEqual(540, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void Evaluate_WithSeveralDutyActivitiesThenLongContinuousRest_DoesNotReturnMissingDailyRestViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T06:00:00Z", "2026-06-17T08:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T09:00:00Z", "2026-06-17T09:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T10:15:00Z", "2026-06-17T11:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T11:15:00Z", "2026-06-17T21:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T05:30:00Z", "2026-06-18T07:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.IsFalse(result.Violations.Any(x => x.Severity == "HIGH"));
        Assert.IsFalse(result.Violations.Any(x =>
            x.Description.Contains("Nie znaleziono ciÄ…gĹ‚ego odpoczynku minimum 9 godzin", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Evaluate_WithLargeTotalRestButNoNineHourContinuousBlock_ReturnsClearMissingContinuousRestViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T00:00:00Z", "2026-06-17T01:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T06:00:00Z", "2026-06-17T07:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T12:00:00Z", "2026-06-17T13:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T18:00:00Z", "2026-06-17T19:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-18T00:30:00Z", "2026-06-18T01:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(300, result.Violations[0].ActualMinutes);
        Assert.IsTrue(result.Violations[0].Description.Contains("Nie znaleziono", StringComparison.Ordinal));
        Assert.AreEqual("2026-06-17T00:00:00.0000000Z", result.Violations[0].Metadata["analysisWindowStartUtc"]);
        Assert.AreEqual("2026-06-18T00:00:00.0000000Z", result.Violations[0].Metadata["analysisWindowEndUtc"]);
        Assert.AreEqual(300L, result.Violations[0].Metadata["longestRestMinutes"]);
        Assert.AreEqual(540L, result.Violations[0].Metadata["requiredReducedRestMinutes"]);
        Assert.AreEqual("MissingContinuousReducedDailyRest", result.Violations[0].Metadata["reason"]);
    }

    [TestMethod]
    public void Evaluate_WithThreePlusNineHourSplitRestInOrder_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T06:00:00Z", "2026-06-17T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T13:00:00Z", "2026-06-17T18:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-18T03:00:00Z", "2026-06-18T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithNinePlusThreeHourSplitRestInWrongOrder_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T06:00:00Z", "2026-06-17T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T19:00:00Z", "2026-06-17T20:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T23:00:00Z", "2026-06-18T00:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.IsTrue(result.Violations.Count >= 1);
        Assert.AreEqual("MEDIUM", result.Violations[0].Severity);
        Assert.AreEqual(540, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void Evaluate_WithThreeHoursAndLessThanNineHoursSplitRest_ReturnsHighViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T06:00:00Z", "2026-06-17T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T13:00:00Z", "2026-06-17T15:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T23:30:00Z", "2026-06-18T00:30:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(510, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void Evaluate_WithLessThanThreeHoursAndNineHoursSplitRest_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T06:00:00Z", "2026-06-17T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T12:30:00Z", "2026-06-17T14:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T23:00:00Z", "2026-06-18T00:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MEDIUM", result.Violations[0].Severity);
        Assert.AreEqual(540, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void Evaluate_WithLessThanNineHourRestBetweenActivities_ReturnsHighViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T00:30:00Z", "2026-06-18T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(510, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void Evaluate_WithActivityCrossingMidnight_UsesRealUtcTimes()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T22:00:00Z", "2026-06-18T03:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T14:00:00Z", "2026-06-18T18:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithRestAndAvailabilitySegments_MergesThemIntoDailyRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T16:00:00Z", "2026-06-17T22:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-17T22:00:00Z", "2026-06-18T03:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T03:00:00Z", "2026-06-18T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

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
