using DriverTime.Domain.Compliance;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using DriverTime.Infrastructure.Services;
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
    public void DailyDrivingLimit_CountsDrivingAcrossMidnightWithoutDailyRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T22:00:00Z", "2026-06-09T02:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-09T08:00:00Z", "2026-06-09T15:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(11 * 60, result.Violations[0].ActualMinutes);
        Assert.AreEqual(10 * 60, result.Violations[0].LimitMinutes);
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
    public void DailyDrivingLimitViolation_DoesNotRequireRuleExecutionTrace()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T18:01:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.IsNull(result.Violations[0].ExecutionTrace);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_DoesNotCountDrivingCoveredByNonDrivingActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = BuildCoveredLongDrivingTimeline(driverId);

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFourHoursThirtyOneMinutesWithoutBreak_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:31:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
        Assert.AreEqual(270, result.Violations[0].LimitMinutes);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFortyFiveMinuteRest_ResetsCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:30:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFortyFiveMinuteBreakActivity_ResetsCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, "BREAK", "2026-06-08T12:30:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithManualFortyFiveMinuteBreak_ResetsCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, "Manual break", "2026-06-08T12:30:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithManualThirtyMinuteBreak_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, "Manual break", "2026-06-08T12:30:00Z", "2026-06-08T13:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:00:00Z", "2026-06-08T13:01:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
        Assert.AreEqual(30L, result.Violations[0].Metadata["receivedBreakMinutes"]);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFourThirtyDrivingAndAdjacentFifteenThirtyRest_ResetsCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:30:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithSplitFifteenAndThirtyMinutes_ResetsCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [DataTestMethod]
    [DataRow(ActivityTypeNormalizer.Rest, ActivityTypeNormalizer.Rest)]
    [DataRow(ActivityTypeNormalizer.Availability, ActivityTypeNormalizer.Availability)]
    [DataRow(ActivityTypeNormalizer.Rest, ActivityTypeNormalizer.Availability)]
    [DataRow(ActivityTypeNormalizer.Availability, ActivityTypeNormalizer.Rest)]
    public void ContinuousDrivingBreak_WithSplitFifteenAndThirtyRestOrAvailability_ResetsCounter(
        string firstBreakType,
        string secondBreakType)
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, firstBreakType, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, secondBreakType, "2026-06-08T12:45:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [DataTestMethod]
    [DataRow(ActivityTypeNormalizer.Rest)]
    [DataRow(ActivityTypeNormalizer.Availability)]
    public void ContinuousDrivingBreak_WithFortyFiveMinuteRestOrAvailability_ResetsCounter(string breakType)
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, breakType, "2026-06-08T12:30:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithSplitThirtyAndFifteenMinutes_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:30:00Z", "2026-06-08T13:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T13:00:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T13:16:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
        Assert.AreEqual(30L, result.Violations[0].Metadata["receivedBreakMinutes"]);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithThreeFifteenMinuteBreaks_DoesNotResetCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T11:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T11:15:00Z", "2026-06-08T11:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T11:30:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:30:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T12:45:00Z", "2026-06-08T13:16:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFifteenAndTwentyMinuteBreaks_DoesNotResetCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:00Z", "2026-06-08T13:05:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:05:00Z", "2026-06-08T13:06:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithWorkBetweenSplitBreakParts_StillAllowsSplitCounterReset()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T10:15:00Z", "2026-06-08T10:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:20:00Z", "2026-06-08T12:50:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:50:00Z", "2026-06-08T13:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:20:00Z", "2026-06-08T13:21:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithAvailabilityBetweenSplitBreakParts_StillAllowsSplitCounterReset()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T10:15:00Z", "2026-06-08T10:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:20:00Z", "2026-06-08T12:50:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:50:00Z", "2026-06-08T13:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:20:00Z", "2026-06-08T13:21:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFifteenThirtySplitAndWorkBetweenDriving_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T11:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T11:15:00Z", "2026-06-08T11:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T11:20:00Z", "2026-06-08T12:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:20:00Z", "2026-06-08T12:50:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithPendingFifteenAndNoSecondThirty_ReturnsViolationAfterLimit()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T11:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T11:15:00Z", "2026-06-08T11:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T11:20:00Z", "2026-06-08T13:20:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(300, result.Violations[0].ActualMinutes);
        Assert.AreEqual(15L, result.Violations[0].Metadata["receivedBreakMinutes"]);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFourteenMinuteFirstPartAndThirtyMinuteBreak_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:14:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:14:00Z", "2026-06-08T11:14:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T11:14:00Z", "2026-06-08T11:44:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T11:44:00Z", "2026-06-08T13:15:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
        Assert.AreEqual(30L, result.Violations[0].Metadata["receivedBreakMinutes"]);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithTwentyMinuteRestShortAvailabilityAndThirtyMinuteRest_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:20:00Z", "2026-06-08T11:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T11:20:00Z", "2026-06-08T11:25:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T11:25:00Z", "2026-06-08T12:25:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:25:00Z", "2026-06-08T12:55:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithWorkAfterFirstSplitPart_DoesNotClearPendingSplit()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:00:00Z", "2026-06-08T12:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T12:15:00Z", "2026-06-08T12:25:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T12:25:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:00Z", "2026-06-08T13:15:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }




    [TestMethod]
    public void ContinuousDrivingBreak_WithViolation_GeneratesDomainRuleExecutionTrace()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:20:00Z", "2026-06-08T13:00:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        var trace = result.Violations[0].ExecutionTrace;
        Assert.IsNotNull(trace);
        Assert.AreEqual("CONTINUOUS_DRIVING_BREAK", trace.RuleCode);
        Assert.AreEqual("Przerwa po 4 godz. 30 min jazdy", trace.RuleName);
        Assert.IsFalse(trace.IsEstimated);
        Assert.IsTrue(trace.Summary.Contains("kierowca prowadzi?", StringComparison.Ordinal));
        Assert.IsTrue(trace.Summary.Contains("Limit", StringComparison.Ordinal));
        Assert.IsTrue(trace.Steps.Any(x => x.Order == 1 && x.Description.Contains("Rozpocz?to analiz?", StringComparison.Ordinal)));
        Assert.IsTrue(trace.Segments.Any(x => x.RestCandidateMinutes == 20 && !x.IsResetPoint));
        Assert.IsTrue(trace.Steps.Any(x => x.IsViolationPoint));
        Assert.IsTrue(trace.Segments.Any(x => x.IsViolationPoint && x.DrivingMinutesAfterSegment == 280));
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithViolationAfterPreviousFortyFiveMinuteBreak_TraceContainsResetStep()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T06:00:00Z", "2026-06-08T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T08:00:00Z", "2026-06-08T08:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:45:00Z", "2026-06-08T13:16:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        var trace = result.Violations[0].ExecutionTrace;
        Assert.IsNotNull(trace);
        Assert.IsTrue(trace.Steps.Any(x => x.IsResetPoint && x.Description.Contains("zresetowano licznik", StringComparison.Ordinal)));
        Assert.IsTrue(trace.Segments.Any(x => x.IsResetPoint && x.RestCandidateMinutes == 45));
        Assert.IsTrue(trace.Steps.Any(x => x.IsViolationPoint));
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithViolation_AddsDiagnosticMetadata()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T11:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T11:15:00Z", "2026-06-08T11:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T11:20:00Z", "2026-06-08T13:20:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        var metadata = result.Violations[0].Metadata;
        Assert.IsFalse(metadata.ContainsKey("analyzedSegments"));
        Assert.IsTrue(metadata.ContainsKey("continuousDrivingMinutes"));
        Assert.IsTrue(metadata.ContainsKey("receivedBreakMinutes"));
        Assert.IsTrue(metadata.ContainsKey("exceededMinutes"));
        Assert.IsTrue(metadata.ContainsKey("firstSplitBreakAccepted"));
        Assert.IsTrue(metadata.ContainsKey("firstSplitBreakMinutes"));
        Assert.IsTrue(metadata.ContainsKey("secondSplitBreakAccepted"));
        Assert.IsTrue(metadata.ContainsKey("splitBreakCompleted"));
        Assert.IsTrue(metadata.ContainsKey("resetReason"));
        Assert.IsTrue(metadata.ContainsKey("violationDetectedAt"));
        Assert.IsTrue(metadata.ContainsKey("debugTrace"));

        Assert.AreEqual(300L, metadata["continuousDrivingMinutes"]);
        Assert.AreEqual(15L, metadata["receivedBreakMinutes"]);
        Assert.AreEqual(30L, metadata["exceededMinutes"]);
        Assert.AreEqual(true, metadata["firstSplitBreakAccepted"]);
        Assert.AreEqual(15L, metadata["firstSplitBreakMinutes"]);
        Assert.AreEqual(false, metadata["secondSplitBreakAccepted"]);
        Assert.AreEqual(false, metadata["splitBreakCompleted"]);
        Assert.AreEqual("NONE", metadata["resetReason"]);

        var trace = metadata["debugTrace"] as List<string>;
        Assert.IsNotNull(trace);
        Assert.IsTrue(trace.Count <= 20);
        Assert.IsTrue(trace.Any(x => x.Contains("triggered violation", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(new DateTime(2026, 6, 8, 13, 20, 0, DateTimeKind.Utc), metadata["violationDetectedAt"]);
    }


    [TestMethod]
    public void ContinuousDrivingBreak_WithTwentyMinuteRestAndFurtherDriving_ReturnsViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:00:00Z", "2026-06-08T12:20:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T12:20:00Z", "2026-06-08T13:00:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(280, result.Violations[0].ActualMinutes);
        Assert.AreEqual(20L, result.Violations[0].Metadata["receivedBreakMinutes"]);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithWorkBetweenDriving_DoesNotResetCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T12:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T12:30:00Z", "2026-06-08T13:01:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFourteenMinutesFiftyNineSecondsAndThirtyMinutes_DoesNotResetCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:14:59Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:14:59Z", "2026-06-08T12:45:59Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:59Z", "2026-06-08T13:15:59Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:59Z", "2026-06-08T13:16:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFifteenMinutesAndTwentyNineMinutesFiftyNineSeconds_DoesNotResetCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T12:45:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:00Z", "2026-06-08T13:14:59Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:14:59Z", "2026-06-08T13:15:59Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual(271, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void ViolationDetectionService_WithSplitFifteenAndThirtyMinutes_DoesNotCreateContinuousDrivingViolation()
    {
        var driverId = Guid.NewGuid();
        var activities = new[]
        {
            DriverActivity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T10:00:00Z"),
            DriverActivity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T10:00:00Z", "2026-06-08T10:15:00Z"),
            DriverActivity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T10:15:00Z", "2026-06-08T12:45:00Z"),
            DriverActivity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T12:45:00Z", "2026-06-08T13:15:00Z"),
            DriverActivity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:45:00Z")
        };

        var violations = DetectLegacyViolations(driverId, activities);

        Assert.IsFalse(violations.Any(x =>
            x.RegulationReference == "EU561:CONTINUOUS_DRIVING_WITHOUT_45M_BREAK"));
    }

    [TestMethod]
    public void ContinuousDrivingBreak_WithFortyFiveMinuteAvailability_ResetsCounter()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T12:30:00Z", "2026-06-08T13:15:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T13:15:00Z", "2026-06-08T17:00:00Z")
        };

        var result = _continuousRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithNineHoursDrivingOnly_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T17:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T17:00:00Z", "2026-06-08T19:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithNineHoursOneMinute_UsesWeeklyExtension()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T17:01:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithTenHoursDriving_UsesWeeklyExtension()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T18:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithMoreThanTenHoursDrivingOnly_ReturnsHighViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T06:00:00Z", "2026-06-08T16:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T16:30:00Z", "2026-06-08T18:30:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(10 * 60 + 30, result.Violations[0].ActualMinutes);
        Assert.AreEqual(10 * 60, result.Violations[0].LimitMinutes);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithTenHoursOneMinute_ReturnsHighViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T18:01:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(10 * 60 + 1, result.Violations[0].ActualMinutes);
        Assert.AreEqual(10 * 60, result.Violations[0].LimitMinutes);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithTwoTenHourDaysInSameWeek_ReturnsNoViolation()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T18:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-09T08:00:00Z", "2026-06-09T18:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithThreeTenHourDaysInSameWeek_ReturnsViolationForThirdExtension()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T18:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-09T08:00:00Z", "2026-06-09T18:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-10T08:00:00Z", "2026-06-10T18:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MEDIUM", result.Violations[0].Severity);
        Assert.AreEqual(10 * 60, result.Violations[0].ActualMinutes);
        Assert.AreEqual(9 * 60, result.Violations[0].LimitMinutes);
        Assert.AreEqual(3, result.Violations[0].Metadata["weeklyExtensionNumber"]);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithWorkBetweenDriving_DoesNotAddWorkTime()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T12:30:00Z", "2026-06-08T14:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T14:30:00Z", "2026-06-08T19:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithAvailabilityBetweenDriving_DoesNotAddAvailabilityTime()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T12:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T12:30:00Z", "2026-06-08T14:30:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T14:30:00Z", "2026-06-08T19:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithNineHourRestBetweenDriving_StartsNewDriverDay()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T14:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-08T14:00:00Z", "2026-06-08T23:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T23:00:00Z", "2026-06-09T05:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithNineHourAvailabilityBetweenDriving_DoesNotStartNewDriverDay()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T14:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Availability, "2026-06-08T14:00:00Z", "2026-06-08T23:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T23:00:00Z", "2026-06-09T05:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(12 * 60, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void DailyDrivingLimit_WithLongWorkBetweenDriving_DoesNotStartNewDriverDay()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T08:00:00Z", "2026-06-08T14:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-08T14:00:00Z", "2026-06-08T23:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-08T23:00:00Z", "2026-06-09T05:00:00Z")
        };

        var result = _dailyRule.Evaluate(driverId, timeline);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("HIGH", result.Violations[0].Severity);
        Assert.AreEqual(12 * 60, result.Violations[0].ActualMinutes);
    }

    [TestMethod]
    public void ComplianceTimelineNormalization_RemovesLongDrivingCoveredByRestAndWork()
    {
        var driverId = Guid.NewGuid();
        var timeline = NormalizeComplianceTimeline(BuildCoveredLongDrivingTimeline(driverId));
        var drivingMinutes = timeline
            .Where(x => x.ActivityType == ActivityTypeNormalizer.Driving)
            .Sum(x => (long)Math.Round(x.Duration.TotalMinutes));

        Assert.AreEqual(32, drivingMinutes);
        Assert.AreEqual(0, _dailyRule.Evaluate(driverId, timeline).Violations.Count);
        Assert.AreEqual(0, _weeklyRule.Evaluate(driverId, timeline).Violations.Count);
        Assert.AreEqual(0, _biWeeklyRule.Evaluate(driverId, timeline).Violations.Count);
        Assert.AreEqual(0, _continuousRule.Evaluate(driverId, timeline).Violations.Count);
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

    private static DriverActivity DriverActivity(
        Guid driverId,
        string activityType,
        string startUtc,
        string endUtc)
    {
        return new DriverActivity
        {
            Id = Guid.NewGuid(),
            DddFileId = Guid.NewGuid(),
            DddFile = new DddFile
            {
                Id = Guid.NewGuid(),
                DriverId = driverId,
                DriverFirstName = "Jan",
                DriverLastName = "Kowalski",
                DriverCardNumber = "CARD"
            },
            ActivityType = activityType,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }

    private static IReadOnlyList<Violation> DetectLegacyViolations(
        Guid driverId,
        IReadOnlyList<DriverActivity> activities)
    {
        var method = typeof(ViolationDetectionService).GetMethod(
            "Detect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.IsNotNull(method);

        return (IReadOnlyList<Violation>)method.Invoke(null, [driverId, activities])!;
    }
}
