using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class TimelineBuilderServiceTests
{
    [TestMethod]
    public void NormalizeTimeline_SortsActivitiesByStart()
    {
        var driverId = Guid.NewGuid();
        var timeline = Normalize([
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T10:00:00Z", "2026-06-17T11:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T09:00:00Z")
        ]);

        Assert.AreEqual(ActivityTypeNormalizer.Driving, timeline[0].ActivityType);
        Assert.AreEqual(ActivityTypeNormalizer.Work, timeline[1].ActivityType);
    }

    [TestMethod]
    public void NormalizeTimeline_MergesAdjacentSameTypeActivities()
    {
        var driverId = Guid.NewGuid();
        var timeline = Normalize([
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T08:00:00Z", "2026-06-17T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T10:00:00Z", "2026-06-17T12:00:00Z")
        ]);

        Assert.AreEqual(1, timeline.Count);
        Assert.AreEqual(new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc), timeline[0].StartUtc);
        Assert.AreEqual(new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc), timeline[0].EndUtc);
    }

    [TestMethod]
    public void NormalizeTimeline_RemovesDrivingOverlapCoveredByRest()
    {
        var driverId = Guid.NewGuid();
        var timeline = Normalize([
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-06-17T08:00:00Z", "2026-06-17T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T09:00:00Z", "2026-06-17T11:00:00Z")
        ]);

        Assert.AreEqual(3, timeline.Count);
        Assert.AreEqual(ActivityTypeNormalizer.Driving, timeline[0].ActivityType);
        Assert.AreEqual(ActivityTypeNormalizer.Rest, timeline[1].ActivityType);
        Assert.AreEqual(ActivityTypeNormalizer.Driving, timeline[2].ActivityType);
        Assert.AreEqual(120, timeline.Where(x => x.ActivityType == ActivityTypeNormalizer.Driving).Sum(x => x.Duration.TotalMinutes));
    }

    [TestMethod]
    public void NormalizeTimeline_KeepsActivityCrossingMidnight()
    {
        var driverId = Guid.NewGuid();
        var timeline = Normalize([
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T22:00:00Z", "2026-06-18T08:00:00Z")
        ]);

        Assert.AreEqual(1, timeline.Count);
        Assert.AreEqual(TimeSpan.FromHours(10), timeline[0].Duration);
    }

    [TestMethod]
    public void NormalizeTimeline_DoesNotTrimActivityThatCrossesAnalysisRangeBoundary()
    {
        var driverId = Guid.NewGuid();
        var timeline = Normalize([
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-16T22:00:00Z", "2026-06-17T08:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T08:00:00Z", "2026-06-17T10:00:00Z")
        ]);

        Assert.AreEqual(new DateTime(2026, 6, 16, 22, 0, 0, DateTimeKind.Utc), timeline[0].StartUtc);
        Assert.AreEqual(new DateTime(2026, 6, 17, 8, 0, 0, DateTimeKind.Utc), timeline[0].EndUtc);
    }

    [TestMethod]
    public void NormalizeTimeline_RestWinsOverOverlappingWork()
    {
        var driverId = Guid.NewGuid();
        var timeline = Normalize([
            Activity(driverId, ActivityTypeNormalizer.Rest, "2026-06-17T18:00:00Z", "2026-06-18T06:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-06-17T20:00:00Z", "2026-06-17T22:00:00Z")
        ]);

        Assert.AreEqual(1, timeline.Count);
        Assert.AreEqual(ActivityTypeNormalizer.Rest, timeline[0].ActivityType);
        Assert.AreEqual(TimeSpan.FromHours(12), timeline[0].Duration);
    }

    private static IReadOnlyList<TimelineActivity> Normalize(
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
