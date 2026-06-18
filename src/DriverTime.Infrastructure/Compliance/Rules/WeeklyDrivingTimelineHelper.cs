using System.Globalization;
using DriverTime.Domain.Compliance;

namespace DriverTime.Infrastructure.Compliance.Rules;

internal static class WeeklyDrivingTimelineHelper
{
    public static IReadOnlyDictionary<DateTime, TimeSpan> GetDrivingByIsoWeek(
        IReadOnlyList<TimelineActivity> timeline)
    {
        var weeklyDriving = new Dictionary<DateTime, TimeSpan>();

        foreach (var activity in timeline
                     .Where(IsDriving)
                     .Where(x => x.StartUtc < x.EndUtc)
                     .OrderBy(x => x.StartUtc)
                     .ThenBy(x => x.EndUtc))
        {
            var segmentStart = activity.StartUtc;

            while (segmentStart < activity.EndUtc)
            {
                var weekStart = GetIsoWeekStart(segmentStart);
                var weekEnd = weekStart.AddDays(7);
                var segmentEnd = activity.EndUtc < weekEnd
                    ? activity.EndUtc
                    : weekEnd;
                var duration = segmentEnd - segmentStart;

                weeklyDriving[weekStart] = weeklyDriving.GetValueOrDefault(weekStart) + duration;
                segmentStart = segmentEnd;
            }
        }

        return weeklyDriving;
    }

    private static bool IsDriving(TimelineActivity activity) =>
        activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase);

    private static DateTime GetIsoWeekStart(DateTime value)
    {
        var year = ISOWeek.GetYear(value);
        var week = ISOWeek.GetWeekOfYear(value);

        return DateTime.SpecifyKind(
            ISOWeek.ToDateTime(year, week, DayOfWeek.Monday),
            DateTimeKind.Utc);
    }
}
