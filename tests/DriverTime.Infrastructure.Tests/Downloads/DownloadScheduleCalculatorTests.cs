using DriverTime.Application.Downloads;
using DriverTime.Application.Downloads.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Downloads;

[TestClass]
public class DownloadScheduleCalculatorTests
{
    [TestMethod]
    public void ActivityEndingToday_UsesActivityEndAsLastDownloadAndKeepsStatusOk()
    {
        var nowUtc = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
        var activityEndUtc = new DateTime(2026, 6, 17, 8, 30, 0, DateTimeKind.Utc);

        var lastActivityUtc = DownloadScheduleCalculator.GetLastActivityUtc(new DateTime?[] { activityEndUtc });
        var nextRequiredUtc = DownloadScheduleCalculator.GetNextRequiredDownloadUtc(
            lastActivityUtc,
            DownloadScheduleCalculator.DriverDownloadIntervalDays);
        var daysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(nextRequiredUtc, nowUtc);

        Assert.AreEqual(activityEndUtc, lastActivityUtc);
        Assert.AreEqual(activityEndUtc.AddDays(28), nextRequiredUtc);
        Assert.AreEqual(28, daysUntilDue);
        Assert.AreEqual(DownloadStatus.Ok, DownloadScheduleCalculator.GetStatus(daysUntilDue));
    }

    [TestMethod]
    public void DueTomorrow_ReturnsWarningWithOneDayUntilDue()
    {
        var nowUtc = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
        var dueUtc = new DateTime(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);

        var daysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(dueUtc, nowUtc);

        Assert.AreEqual(1, daysUntilDue);
        Assert.AreEqual(DownloadStatus.Warning, DownloadScheduleCalculator.GetStatus(daysUntilDue));
    }

    [TestMethod]
    public void DueInMoreThanSevenDaysWithPartialDay_ReturnsOk()
    {
        var nowUtc = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
        var dueUtc = new DateTime(2026, 6, 24, 13, 0, 0, DateTimeKind.Utc);

        var daysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(dueUtc, nowUtc);

        Assert.AreEqual(8, daysUntilDue);
        Assert.AreEqual(DownloadStatus.Ok, DownloadScheduleCalculator.GetStatus(daysUntilDue));
    }

    [TestMethod]
    public void DueYesterday_ReturnsOverdue()
    {
        var nowUtc = new DateTime(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);
        var dueUtc = new DateTime(2026, 6, 16, 23, 0, 0, DateTimeKind.Utc);

        var daysUntilDue = DownloadScheduleCalculator.GetDaysUntilDue(dueUtc, nowUtc);

        Assert.AreEqual(-1, daysUntilDue);
        Assert.AreEqual(DownloadStatus.Overdue, DownloadScheduleCalculator.GetStatus(daysUntilDue));
    }

    [TestMethod]
    public void ActivityCrossingMidnight_UsesEndUtc()
    {
        var startUtc = new DateTime(2026, 6, 16, 22, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 6, 17, 2, 15, 0, DateTimeKind.Utc);

        var lastActivityUtc = DownloadScheduleCalculator.GetLastActivityUtc(new DateTime?[] { startUtc, endUtc });

        Assert.AreEqual(endUtc, lastActivityUtc);
    }

    [TestMethod]
    public void MultipleImportsSameDay_UsesLatestActivityEnd()
    {
        var firstFileActivityEndUtc = new DateTime(2026, 6, 17, 9, 0, 0, DateTimeKind.Utc);
        var secondFileActivityEndUtc = new DateTime(2026, 6, 17, 18, 45, 0, DateTimeKind.Utc);

        var lastActivityUtc = DownloadScheduleCalculator.GetLastActivityUtc(
        new DateTime?[]
        {
            firstFileActivityEndUtc,
            secondFileActivityEndUtc
        });

        Assert.AreEqual(secondFileActivityEndUtc, lastActivityUtc);
    }

    [TestMethod]
    public void MultipleFilesForSameDriver_UsesLatestActivityEndInsteadOfImportOrder()
    {
        var olderUploadedLaterActivityEndUtc = new DateTime(2026, 5, 20, 17, 0, 0, DateTimeKind.Utc);
        var newerActivityEndUtc = new DateTime(2026, 6, 10, 14, 30, 0, DateTimeKind.Utc);

        var lastActivityUtc = DownloadScheduleCalculator.GetLastActivityUtc(
        new DateTime?[]
        {
            olderUploadedLaterActivityEndUtc,
            newerActivityEndUtc
        });

        Assert.AreEqual(newerActivityEndUtc, lastActivityUtc);
    }

    [TestMethod]
    public void MultipleFilesForSameVehicle_UsesLatestVehicleUseEnd()
    {
        var firstVehicleUseEndUtc = new DateTime(2026, 4, 1, 16, 0, 0, DateTimeKind.Utc);
        var secondVehicleUseEndUtc = new DateTime(2026, 4, 3, 7, 30, 0, DateTimeKind.Utc);
        var duplicateOlderUseEndUtc = new DateTime(2026, 4, 1, 16, 0, 0, DateTimeKind.Utc);

        var lastActivityUtc = DownloadScheduleCalculator.GetLastActivityUtc(
        new DateTime?[]
        {
            firstVehicleUseEndUtc,
            secondVehicleUseEndUtc,
            duplicateOlderUseEndUtc
        });

        Assert.AreEqual(secondVehicleUseEndUtc, lastActivityUtc);
    }
}
