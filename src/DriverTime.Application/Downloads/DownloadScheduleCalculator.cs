using DriverTime.Application.Downloads.DTOs;

namespace DriverTime.Application.Downloads;

public static class DownloadScheduleCalculator
{
    public const int DriverDownloadIntervalDays = 28;
    public const int VehicleDownloadIntervalDays = 90;
    public const int WarningDays = 7;

    public static DateTime? GetLastActivityUtc(IEnumerable<DateTime?> activityEndDatesUtc)
    {
        return activityEndDatesUtc
            .Where(x => x.HasValue)
            .Max();
    }

    public static DateTime? GetNextRequiredDownloadUtc(
        DateTime? lastActivityUtc,
        int intervalDays)
    {
        return lastActivityUtc?.AddDays(intervalDays);
    }

    public static int? GetDaysUntilDue(
        DateTime? nextRequiredDownloadUtc,
        DateTime nowUtc)
    {
        if (!nextRequiredDownloadUtc.HasValue)
        {
            return null;
        }

        var totalDays = (nextRequiredDownloadUtc.Value - nowUtc).TotalDays;

        if (totalDays == 0)
        {
            return 0;
        }

        return totalDays > 0
            ? (int)Math.Ceiling(totalDays)
            : (int)Math.Floor(totalDays);
    }

    public static string GetStatus(int? daysUntilDue)
    {
        if (!daysUntilDue.HasValue || daysUntilDue.Value < 0)
        {
            return DownloadStatus.Overdue;
        }

        return daysUntilDue.Value <= WarningDays
            ? DownloadStatus.Warning
            : DownloadStatus.Ok;
    }
}
