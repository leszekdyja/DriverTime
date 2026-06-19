using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class SixTwentyFourHourPeriodsRuleTests
{
    private readonly SixTwentyFourHourPeriodsRule _rule = new(NullLogger<SixTwentyFourHourPeriodsRule>.Instance);

    [TestMethod]
    public void Evaluate_WhenNextWeeklyRestStartsBeforeDeadline_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildTimelineWithNextWeeklyRest(
            driverId,
            nextWeeklyRestStartUtc: DateTime.Parse("2026-06-14T23:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WhenNextWeeklyRestStartsExactlyAtDeadline_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildTimelineWithNextWeeklyRest(
            driverId,
            nextWeeklyRestStartUtc: DateTime.Parse("2026-06-15T00:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WhenNextWeeklyRestStartsAfterDeadline_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildTimelineWithNextWeeklyRest(
            driverId,
            nextWeeklyRestStartUtc: DateTime.Parse("2026-06-15T02:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("SIX_24H_PERIODS", result.Violations[0].Code);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(120L, result.Violations[0].Metadata["exceededMinutes"]);
        Assert.AreNotEqual(0L, result.Violations[0].Metadata["nextWeeklyRestStartUtc"]);
    }

    [TestMethod]
    public void Evaluate_WhenNoNextWeeklyRestAndTimelinePassesDeadline_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildTimelineWithoutNextWeeklyRest(
            driverId,
            timelineEndUtc: DateTime.Parse("2026-06-15T22:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(22L * 60, result.Violations[0].Metadata["exceededMinutes"]);
        Assert.AreEqual(0L, result.Violations[0].Metadata["nextWeeklyRestStartUtc"]);
    }

    [TestMethod]
    public void Evaluate_WhenThereIsNoPreviousWeeklyRest_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new List<TimelineActivity>
        {
            Work(driverId, "2026-06-09T08:00:00Z", "2026-06-09T12:00:00Z"),
            Work(driverId, "2026-06-10T08:00:00Z", "2026-06-10T12:00:00Z"),
            Rest(driverId, "2026-06-11T08:00:00Z", "2026-06-11T16:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithMultipleConsecutiveWeeks_ChecksEachPeriod()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildTimelineWithNextWeeklyRest(
            driverId,
            nextWeeklyRestStartUtc: DateTime.Parse("2026-06-15T00:00:00Z").ToUniversalTime(),
            followingWeeklyRestStartUtc: DateTime.Parse("2026-06-21T23:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    private static List<TimelineActivity> BuildTimelineWithNextWeeklyRest(
        Guid driverId,
        DateTime nextWeeklyRestStartUtc,
        DateTime? followingWeeklyRestStartUtc = null)
    {
        var timeline = new List<TimelineActivity>
        {
            Rest(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z")
        };

        timeline.Add(Work(
            driverId,
            DateTime.Parse("2026-06-09T00:00:00Z").ToUniversalTime(),
            nextWeeklyRestStartUtc));
        timeline.Add(Rest(driverId, nextWeeklyRestStartUtc, nextWeeklyRestStartUtc.AddHours(24)));

        if (followingWeeklyRestStartUtc.HasValue)
        {
            timeline.Add(Work(driverId, nextWeeklyRestStartUtc.AddHours(24), followingWeeklyRestStartUtc.Value));
            timeline.Add(Rest(driverId, followingWeeklyRestStartUtc.Value, followingWeeklyRestStartUtc.Value.AddHours(24)));
        }

        return timeline;
    }

    private static List<TimelineActivity> BuildTimelineWithoutNextWeeklyRest(
        Guid driverId,
        DateTime timelineEndUtc)
    {
        var timeline = new List<TimelineActivity>
        {
            Rest(driverId, "2026-06-08T00:00:00Z", "2026-06-09T00:00:00Z")
        };

        timeline.Add(Work(
            driverId,
            DateTime.Parse("2026-06-09T00:00:00Z").ToUniversalTime(),
            timelineEndUtc));

        return timeline;
    }

    private static TimelineActivity Rest(Guid driverId, string startUtc, string endUtc)
    {
        return Rest(
            driverId,
            DateTime.Parse(startUtc).ToUniversalTime(),
            DateTime.Parse(endUtc).ToUniversalTime());
    }

    private static TimelineActivity Rest(Guid driverId, DateTime startUtc, DateTime endUtc)
    {
        return Activity(driverId, ActivityTypeNormalizer.Rest, startUtc, endUtc);
    }

    private static TimelineActivity Work(Guid driverId, string startUtc, string endUtc)
    {
        return Work(
            driverId,
            DateTime.Parse(startUtc).ToUniversalTime(),
            DateTime.Parse(endUtc).ToUniversalTime());
    }

    private static TimelineActivity Work(Guid driverId, DateTime startUtc, DateTime endUtc)
    {
        return Activity(driverId, ActivityTypeNormalizer.Work, startUtc, endUtc);
    }

    private static TimelineActivity Activity(
        Guid driverId,
        string activityType,
        DateTime startUtc,
        DateTime endUtc)
    {
        return new TimelineActivity
        {
            SourceActivityId = Guid.NewGuid(),
            DriverId = driverId,
            ActivityType = activityType,
            StartUtc = startUtc,
            EndUtc = endUtc
        };
    }
}
