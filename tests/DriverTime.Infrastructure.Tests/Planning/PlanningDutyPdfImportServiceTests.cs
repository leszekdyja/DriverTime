using DriverTime.Infrastructure.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Planning;

[TestClass]
public class PlanningDutyPdfImportServiceTests
{
    [TestMethod]
    public void ParseText_EmptyText_ReturnsWarningWithoutException()
    {
        var warnings = new List<string>();

        var duties = PlanningDutyPdfImportService.ParseText(string.Empty, "pusty.pdf", warnings);

        Assert.AreEqual(0, duties.Count);
        Assert.IsTrue(warnings.Any(x => x.Contains("Nie rozpoznano służb", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ParseText_SimpleDutyText_ReturnsDutyPreviewWithConfidence()
    {
        const string text = "Służba 101\nStart 05:20 - Koniec 13:45\nLinie: K-11 K-19bis\nCzas pracy 08:25\nPrzerwy 45\nJazda 320\nPrzebieg km: 112,5";
        var warnings = new List<string>();

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzby.pdf", warnings);

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("101", duties[0].DutyNumber);
        Assert.AreEqual("Służba 101", duties[0].Name);
        Assert.AreEqual(new TimeOnly(5, 20), duties[0].StartTime);
        Assert.AreEqual(new TimeOnly(13, 45), duties[0].EndTime);
        Assert.AreEqual(505, duties[0].WorkMinutes);
        Assert.AreEqual(45, duties[0].BreakMinutes);
        Assert.AreEqual(320, duties[0].DrivingMinutes);
        Assert.AreEqual(112.5m, duties[0].DistanceKm);
        Assert.IsTrue(duties[0].Lines.Any(x => x.LineCode == "K-11"));
        Assert.IsTrue(duties[0].Lines.Any(x => x.LineCode == "K-19bis"));
        Assert.AreEqual(100, duties[0].Confidence.DutyNumber);
        Assert.AreEqual(90, duties[0].Confidence.StartTime);
        Assert.AreEqual(90, duties[0].Confidence.EndTime);
        Assert.AreEqual(90, duties[0].Confidence.Line);
        Assert.AreEqual(90, duties[0].Confidence.WorkingMinutes);
        Assert.AreEqual(90, duties[0].Confidence.DrivingMinutes);
        Assert.AreEqual(90, duties[0].Confidence.BreakMinutes);
        Assert.AreEqual(75, duties[0].Confidence.DistanceKm);
        Assert.AreEqual(0, warnings.Count);
    }

    [TestMethod]
    public void ParseText_DutyWithMissingFields_ReturnsZeroConfidenceForEmptyFields()
    {
        const string text = "Służba 202";
        var warnings = new List<string>();

        var duties = PlanningDutyPdfImportService.ParseText(text, "czesciowy.pdf", warnings);

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("202", duties[0].DutyNumber);
        Assert.IsNull(duties[0].StartTime);
        Assert.IsNull(duties[0].EndTime);
        Assert.IsNull(duties[0].WorkMinutes);
        Assert.AreEqual(100, duties[0].Confidence.DutyNumber);
        Assert.AreEqual(0, duties[0].Confidence.StartTime);
        Assert.AreEqual(0, duties[0].Confidence.EndTime);
        Assert.AreEqual(0, duties[0].Confidence.Line);
        Assert.AreEqual(0, duties[0].Confidence.Stops);
        Assert.AreEqual(0, duties[0].Confidence.WorkingMinutes);
        Assert.AreEqual(0, duties[0].Confidence.DrivingMinutes);
        Assert.AreEqual(0, duties[0].Confidence.BreakMinutes);
        Assert.AreEqual(0, duties[0].Confidence.DistanceKm);
        Assert.AreEqual(0, warnings.Count);
    }
}
