using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class ReducedWeeklyRestCompensationRuleTests
{
    private readonly ReducedWeeklyRestCompensationRule _rule = new(NullLogger<ReducedWeeklyRestCompensationRule>.Instance);

    [TestMethod]
    public void Evaluate_WithRegularFortyFiveHourWeeklyRest_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new List<TimelineActivity>
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T08:00:00Z", "2026-06-08T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-10T13:00:00Z", "2026-06-10T17:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithTwentyFourHourReducedRestAndCompensationBeforeDeadline_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildBusyTimelineAfterReducedRest(
            driverId,
            firstDutyEndUtc: DateTime.Parse("2026-06-08T16:00:00Z").ToUniversalTime(),
            nextDutyStartUtc: DateTime.Parse("2026-06-09T16:00:00Z").ToUniversalTime(),
            busyUntilUtc: DateTime.Parse("2026-06-23T08:00:00Z").ToUniversalTime());

        timeline.Add(Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-24T14:00:00Z", "2026-06-24T18:00:00Z"));
        timeline.AddRange(BuildBusyTimeline(
            driverId,
            DateTime.Parse("2026-06-24T18:00:00Z").ToUniversalTime(),
            DateTime.Parse("2026-07-07T08:00:00Z").ToUniversalTime()));

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithTwentyFourHourReducedRestWithoutCompensation_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildBusyTimelineAfterReducedRest(
            driverId,
            firstDutyEndUtc: DateTime.Parse("2026-06-08T16:00:00Z").ToUniversalTime(),
            nextDutyStartUtc: DateTime.Parse("2026-06-09T16:00:00Z").ToUniversalTime(),
            busyUntilUtc: DateTime.Parse("2026-07-07T08:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(21 * 60, result.Violations[0].LimitMinutes);
        Assert.AreEqual(0, result.Violations[0].ActualMinutes);
        Assert.AreEqual(24L * 60, result.Violations[0].Metadata["reducedRestMinutes"]);
        Assert.AreEqual(45L * 60, result.Violations[0].Metadata["requiredRegularWeeklyRestMinutes"]);
        Assert.AreEqual(21L * 60, result.Violations[0].Metadata["compensationDebtMinutes"]);
        Assert.IsTrue(result.Violations[0].Metadata.ContainsKey("compensationDeadlineUtc"));
    }

    [TestMethod]
    public void Evaluate_WithCompensationAfterDeadline_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildBusyTimelineAfterReducedRest(
            driverId,
            firstDutyEndUtc: DateTime.Parse("2026-06-08T16:00:00Z").ToUniversalTime(),
            nextDutyStartUtc: DateTime.Parse("2026-06-09T16:00:00Z").ToUniversalTime(),
            busyUntilUtc: DateTime.Parse("2026-07-07T08:00:00Z").ToUniversalTime());

        timeline.Add(Activity(driverId, ActivityTypeNormalizer.Work, "2026-07-08T14:00:00Z", "2026-07-08T18:00:00Z"));

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("High", result.Violations[0].Severity);
        Assert.AreEqual(21 * 60, result.Violations[0].LimitMinutes);
    }

    [TestMethod]
    public void Evaluate_WithRestShorterThanTwentyFourHours_IsIgnoredByCompensationRule()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildBusyTimelineAfterReducedRest(
            driverId,
            firstDutyEndUtc: DateTime.Parse("2026-06-08T16:00:00Z").ToUniversalTime(),
            nextDutyStartUtc: DateTime.Parse("2026-06-09T15:00:00Z").ToUniversalTime(),
            busyUntilUtc: DateTime.Parse("2026-07-07T08:00:00Z").ToUniversalTime());

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    private static List<TimelineActivity> BuildBusyTimelineAfterReducedRest(
        Guid driverId,
        DateTime firstDutyEndUtc,
        DateTime nextDutyStartUtc,
        DateTime busyUntilUtc)
    {
        var timeline = new List<TimelineActivity>
        {
            Activity(driverId, ActivityTypeNormalizer.Work, firstDutyEndUtc.AddHours(-4), firstDutyEndUtc),
            Activity(driverId, ActivityTypeNormalizer.Driving, nextDutyStartUtc, nextDutyStartUtc.AddHours(4))
        };

        timeline.AddRange(BuildBusyTimeline(driverId, nextDutyStartUtc.AddHours(4), busyUntilUtc));

        return timeline;
    }

    private static IEnumerable<TimelineActivity> BuildBusyTimeline(
        Guid driverId,
        DateTime startUtc,
        DateTime endUtc)
    {
        var activities = new List<TimelineActivity>();
        var cursor = startUtc;
        var index = 0;

        while (cursor < endUtc)
        {
            var activityStart = cursor.AddHours(8);
            if (activityStart >= endUtc)
            {
                break;
            }

            var activityEnd = activityStart.AddHours(4);
            if (activityEnd > endUtc)
            {
                activityEnd = endUtc;
            }

            activities.Add(Activity(
                driverId,
                index % 2 == 0 ? ActivityTypeNormalizer.Work : ActivityTypeNormalizer.Driving,
                activityStart,
                activityEnd));

            cursor = activityEnd;
            index++;
        }

        return activities;
    }

    private static TimelineActivity Activity(
        Guid driverId,
        string activityType,
        string startUtc,
        string endUtc)
    {
        return Activity(
            driverId,
            activityType,
            DateTime.Parse(startUtc).ToUniversalTime(),
            DateTime.Parse(endUtc).ToUniversalTime());
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
