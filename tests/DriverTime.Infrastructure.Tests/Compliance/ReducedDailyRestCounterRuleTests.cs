using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class ReducedDailyRestCounterRuleTests
{
    private readonly ReducedDailyRestCounterRule _rule = new(NullLogger<ReducedDailyRestCounterRule>.Instance);

    [TestMethod]
    public void Evaluate_WithThreeReducedDailyRestsBetweenWeeklyRests_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Work(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Work(driverId, "2026-06-09T09:00:00Z", "2026-06-10T00:00:00Z"),
            Work(driverId, "2026-06-10T09:00:00Z", "2026-06-11T00:00:00Z"),
            Work(driverId, "2026-06-11T09:00:00Z", "2026-06-12T00:00:00Z"),
            Work(driverId, "2026-06-13T00:00:00Z", "2026-06-13T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithFourReducedDailyRestsBetweenWeeklyRests_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Work(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Work(driverId, "2026-06-09T09:00:00Z", "2026-06-10T00:00:00Z"),
            Work(driverId, "2026-06-10T09:00:00Z", "2026-06-11T00:00:00Z"),
            Work(driverId, "2026-06-11T09:00:00Z", "2026-06-12T00:00:00Z"),
            Work(driverId, "2026-06-12T09:00:00Z", "2026-06-13T00:00:00Z"),
            Work(driverId, "2026-06-14T00:00:00Z", "2026-06-14T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(4, result.Violations[0].ActualMinutes);
        Assert.AreEqual(3, result.Violations[0].LimitMinutes);
        Assert.AreEqual(4, result.Violations[0].Metadata["reducedDailyRestCount"]);
        Assert.AreEqual(3, result.Violations[0].Metadata["allowedReducedDailyRestCount"]);
        Assert.AreEqual(9 * 60, result.Violations[0].Metadata["violatingRestMinutes"]);
    }

    [TestMethod]
    public void Evaluate_WithRegularDailyRest_DoesNotIncreaseCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Work(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Work(driverId, "2026-06-09T09:00:00Z", "2026-06-10T00:00:00Z"),
            Work(driverId, "2026-06-10T09:00:00Z", "2026-06-11T00:00:00Z"),
            Work(driverId, "2026-06-11T11:00:00Z", "2026-06-12T00:00:00Z"),
            Work(driverId, "2026-06-12T09:00:00Z", "2026-06-13T00:00:00Z"),
            Work(driverId, "2026-06-14T00:00:00Z", "2026-06-14T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithSplitThreePlusNineRest_DoesNotIncreaseCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Work(driverId, "2026-06-08T00:00:00Z", "2026-06-08T10:00:00Z"),
            Work(driverId, "2026-06-08T13:00:00Z", "2026-06-09T00:00:00Z"),
            Work(driverId, "2026-06-09T09:00:00Z", "2026-06-10T00:00:00Z"),
            Work(driverId, "2026-06-10T09:00:00Z", "2026-06-11T00:00:00Z"),
            Work(driverId, "2026-06-11T09:00:00Z", "2026-06-12T00:00:00Z"),
            Work(driverId, "2026-06-13T00:00:00Z", "2026-06-13T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithRestShorterThanNineHours_DoesNotIncreaseCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Work(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Work(driverId, "2026-06-09T09:00:00Z", "2026-06-10T00:00:00Z"),
            Work(driverId, "2026-06-10T09:00:00Z", "2026-06-11T00:00:00Z"),
            Work(driverId, "2026-06-11T17:30:00Z", "2026-06-12T00:00:00Z"),
            Work(driverId, "2026-06-12T09:00:00Z", "2026-06-13T00:00:00Z"),
            Work(driverId, "2026-06-14T00:00:00Z", "2026-06-14T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_ResetsCounterAfterWeeklyRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Work(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z"),
            Work(driverId, "2026-06-09T09:00:00Z", "2026-06-10T00:00:00Z"),
            Work(driverId, "2026-06-10T09:00:00Z", "2026-06-11T00:00:00Z"),
            Work(driverId, "2026-06-12T00:00:00Z", "2026-06-12T04:00:00Z"),
            Work(driverId, "2026-06-13T04:00:00Z", "2026-06-14T00:00:00Z"),
            Work(driverId, "2026-06-14T09:00:00Z", "2026-06-15T00:00:00Z"),
            Work(driverId, "2026-06-15T09:00:00Z", "2026-06-16T00:00:00Z"),
            Work(driverId, "2026-06-16T09:00:00Z", "2026-06-17T00:00:00Z"),
            Work(driverId, "2026-06-18T00:00:00Z", "2026-06-18T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    private static TimelineActivity Work(Guid driverId, string startUtc, string endUtc)
    {
        return new TimelineActivity
        {
            SourceActivityId = Guid.NewGuid(),
            DriverId = driverId,
            ActivityType = ActivityTypeNormalizer.Work,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }
}
