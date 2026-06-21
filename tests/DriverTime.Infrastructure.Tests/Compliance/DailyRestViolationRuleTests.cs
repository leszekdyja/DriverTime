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
        Assert.AreEqual(300L, result.Violations[0].Metadata["actualRestMinutes"]);
        Assert.AreEqual(540L, result.Violations[0].Metadata["requiredRestMinutes"]);
        Assert.AreEqual(240L, result.Violations[0].Metadata["missingRestMinutes"]);
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
    public void Evaluate_WithRestCrossingMidnightLongerThanNineHours_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-19T12:00:00Z", "2026-06-19T18:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T18:00:00Z", "2026-06-20T08:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T09:00:00Z", "2026-06-20T10:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithRestStartingBeforeAnalyzedDayAndEndingInDay_IsIncluded()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-18T16:00:00Z", "2026-06-18T18:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-18T18:00:00Z", "2026-06-19T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-19T08:30:00Z", "2026-06-19T09:30:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithElevenHourRestEndingSecondsBeforeWindowStart_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T19:08:13Z", "2026-06-20T06:08:12Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T06:09:00Z", "2026-06-20T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-20T14:00:00Z", "2026-06-20T19:32:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T19:32:00Z", "2026-06-20T20:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithNineHourRestEndingSecondsBeforeWindowStart_DoesNotReturnHighViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T21:08:12Z", "2026-06-20T06:08:12Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T06:09:00Z", "2026-06-20T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-20T14:00:00Z", "2026-06-20T19:32:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T19:32:00Z", "2026-06-20T20:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.IsFalse(result.Violations.Any(x => x.Severity == "HIGH"));
    }

    [TestMethod]
    public void Evaluate_WithRestCrossingWindowStart_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-19T12:00:00Z", "2026-06-19T19:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T19:00:00Z", "2026-06-20T06:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T06:09:00Z", "2026-06-20T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithRealJuneTwentiethBoundaryRest_DoesNotReturnDailyRestViolation()
    {
        var driverId = Guid.Parse("d27eaace-5563-48cc-a4a5-f9eb07ac9b22");
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-19T16:00:00Z", "2026-06-19T19:08:13Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T19:08:13Z", "2026-06-20T06:08:12Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-20T06:09:00Z", "2026-06-20T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T10:00:00Z", "2026-06-20T11:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-20T11:00:00Z", "2026-06-20T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-20T12:56:00Z", "2026-06-20T14:10:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T14:10:00Z", "2026-06-20T18:28:22Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-20T18:28:22Z", "2026-06-21T00:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.IsFalse(result.Violations.Any(x =>
            x.Severity == "HIGH" &&
            x.Metadata.TryGetValue("analysisWindowStartUtc", out var value) &&
            value?.ToString() == "2026-06-20T06:09:00.0000000Z"));
    }

    [TestMethod]
    public void Evaluate_WithOnlyFiveHoursThirtyTwoMinutesInsideWindow_UsesPreviousBoundaryRest()
    {
        var driverId = Guid.Parse("d27eaace-5563-48cc-a4a5-f9eb07ac9b22");
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-19T16:00:00Z", "2026-06-19T19:08:13Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T19:08:13Z", "2026-06-20T06:08:12Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-20T06:09:00Z", "2026-06-20T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T08:00:00Z", "2026-06-20T18:28:22Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-20T18:28:22Z", "2026-06-21T00:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-21T00:00:00Z", "2026-06-21T01:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.IsFalse(result.Violations.Any(x =>
            x.Severity == "HIGH" &&
            x.Metadata.TryGetValue("analysisWindowStartUtc", out var value) &&
            value?.ToString() == "2026-06-20T06:09:00.0000000Z"));
        Assert.IsFalse(result.Violations.Any(x =>
            x.Metadata.TryGetValue("analysisWindowStartUtc", out var start) &&
            start?.ToString() == "2026-06-20T06:09:00.0000000Z" &&
            x.Metadata.TryGetValue("longestRestMinutes", out var value) &&
            value is 332L));
    }

    [TestMethod]
    public void Evaluate_WithRestCrossingMidnightAndDuplicatedWorkOverlay_KeepsContinuousRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = NormalizeComplianceTimeline([
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-19T12:00:00Z", "2026-06-19T18:28:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-19T18:28:00Z", "2026-06-20T13:19:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T00:00:00Z", "2026-06-20T07:47:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-20T13:19:00Z", "2026-06-20T14:00:00Z")
        ]);

        var longestRest = timeline
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Rest)
            .Max(x => x.Duration);
        var result = _rule.Evaluate(driverId, timeline);

        Assert.IsTrue(longestRest >= TimeSpan.FromHours(18));
        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithNoDutyActivitiesInFetchedRange_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T00:00:00Z", "2026-06-17T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-17T08:00:00Z", "2026-06-17T10:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithRestEndingAfterFetchedRange_ReturnsNoViolationWhenIncludedInTimeline()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T16:00:00Z", "2026-06-18T04:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithRestAndAvailabilitySegments_DoesNotMergeAvailabilityIntoDailyRest()
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

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(360, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void Evaluate_WithWorkBetweenRestSegments_DoesNotMergeThemIntoDailyRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T16:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T16:00:00Z", "2026-06-17T22:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T22:00:00Z", "2026-06-17T23:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T23:00:00Z", "2026-06-18T04:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-18T04:00:00Z", "2026-06-18T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(360, result.Violations[0].ActualMinutes);
    }

    private static IReadOnlyList<TimelineActivity> NormalizeComplianceTimeline(
        IEnumerable<TimelineActivity> timeline)
    {
        var method = typeof(TimelineBuilderService).GetMethod(
            "NormalizeTimeline",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.IsNotNull(method);

        return (IReadOnlyList<TimelineActivity>)method.Invoke(null, [timeline])!;
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
