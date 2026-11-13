using System.Reflection;
using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class TimelineNormalizationIndexTests
{
    [TestMethod]
    public void LargeHistoryKeepsEverySeparatedIntervalAndCrewStatus()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var activities = Enumerable.Range(0, 62000).Select(index => new TimelineActivity
        {
            SourceActivityId = Guid.NewGuid(), ActivityType = "DRIVING",
            StartUtc = start.AddMinutes(index * 2), EndUtc = start.AddMinutes(index * 2 + 1),
            IsCrew = index % 2 == 0, CardSlot = index % 2 + 1
        }).ToList();

        var result = Normalize(activities);

        Assert.AreEqual(activities.Count, result.Count);
        for (var index = 0; index < activities.Count; index++)
        {
            Assert.AreEqual(activities[index].SourceActivityId, result[index].SourceActivityId);
            Assert.AreEqual(activities[index].StartUtc, result[index].StartUtc);
            Assert.AreEqual(activities[index].EndUtc, result[index].EndUtc);
            Assert.AreEqual(activities[index].IsCrew, result[index].IsCrew);
            Assert.AreEqual(activities[index].CardSlot, result[index].CardSlot);
        }
    }

    [TestMethod]
    public void OverlappingDownloadsPreserveNewestCoverageAndActivityPriorityAtEveryMinute()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var random = new Random(321);
        var types = new[] { "UNKNOWN", "DRIVING", "WORK", "AVAILABILITY", "REST" };
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var activities = Enumerable.Range(0, 100).Select(_ =>
            {
                var minute = random.Next(120);
                return new TimelineActivity
                {
                    SourceActivityId = Guid.NewGuid(), ActivityType = types[random.Next(types.Length)],
                    StartUtc = start.AddMinutes(minute), EndUtc = start.AddMinutes(minute + random.Next(1, 30)),
                    SourceCoverageEndUtc = start.AddDays(random.Next(1, 5)),
                    IsCrew = random.Next(2) == 0 ? null : true, CardSlot = 2
                };
            }).ToList();
            var result = Normalize(activities);
            for (var minute = 0; minute < 150; minute++)
            {
                var instant = start.AddMinutes(minute);
                var expected = activities.Where(x => x.StartUtc <= instant && x.EndUtc > instant)
                    .OrderByDescending(x => x.SourceCoverageEndUtc)
                    .ThenByDescending(x => Array.IndexOf(types, x.ActivityType))
                    .ThenByDescending(x => x.IsCrew.HasValue)
                    .ThenBy(x => x.StartUtc).ThenBy(x => x.EndUtc).FirstOrDefault();
                var actual = result.SingleOrDefault(x => x.StartUtc <= instant && x.EndUtc > instant);
                Assert.AreEqual(expected?.ActivityType, actual?.ActivityType);
                Assert.AreEqual(expected?.IsCrew, actual?.IsCrew);
                // Adjacent intervals of the same type/status may be merged across downloads.
                Assert.AreEqual(expected?.CardSlot, actual?.CardSlot);
            }
        }
    }

    private static IReadOnlyList<TimelineActivity> Normalize(IEnumerable<TimelineActivity> activities) =>
        (IReadOnlyList<TimelineActivity>)typeof(TimelineBuilderService)
            .GetMethod("NormalizeTimeline", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { activities })!;
}
