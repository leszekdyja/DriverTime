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
        const string text = "Służba 101\nStart 05:20 koniec 13:45\nLinia K-11\nLinia K-19bis\nCzas pracy 08:25\nPrzerwa 45 min\nJazda 320 min\nDystans 112,5";
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
        Assert.AreEqual(80, duties[0].Confidence.DutyNumber);
        Assert.AreEqual(100, duties[0].Confidence.StartTime);
        Assert.AreEqual(100, duties[0].Confidence.EndTime);
        Assert.AreEqual(100, duties[0].Confidence.Line);
        Assert.AreEqual(100, duties[0].Confidence.WorkingMinutes);
        Assert.AreEqual(100, duties[0].Confidence.DrivingMinutes);
        Assert.AreEqual(100, duties[0].Confidence.BreakMinutes);
        Assert.AreEqual(100, duties[0].Confidence.DistanceKm);
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
        Assert.AreEqual(80, duties[0].Confidence.DutyNumber);
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

    [TestMethod]
    public void ParseText_NrSluzbyVariant_RecognizesDutyNumber()
    {
        const string text = "Nr służby: 101\n05:20 - 13:45";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("101", duties[0].DutyNumber);
        Assert.AreEqual(100, duties[0].Confidence.DutyNumber);
    }

    [TestMethod]
    public void ParseText_DottedTimeRange_RecognizesStartAndEnd()
    {
        const string text = "Służba 101\n05.20 - 13.45";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.AreEqual(new TimeOnly(5, 20), duties[0].StartTime);
        Assert.AreEqual(new TimeOnly(13, 45), duties[0].EndTime);
        Assert.AreEqual(80, duties[0].Confidence.StartTime);
    }

    [TestMethod]
    public void ParseText_HoursAndMinutesDuration_RecognizesWorkMinutes()
    {
        const string text = "Służba 101\nCzas pracy 8 h 25 min";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.AreEqual(505, duties[0].WorkMinutes);
        Assert.AreEqual(100, duties[0].Confidence.WorkingMinutes);
    }

    [TestMethod]
    public void ParseText_MinutesDuration_RecognizesWorkMinutes()
    {
        const string text = "Służba 101\nPraca 505 min";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.AreEqual(505, duties[0].WorkMinutes);
    }

    [TestMethod]
    public void ParseText_LineLabel_RecognizesLine()
    {
        const string text = "Służba 101\nLinia 299";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.IsTrue(duties[0].Lines.Any(x => x.LineCode == "299"));
        Assert.AreEqual(100, duties[0].Confidence.Line);
    }

    [TestMethod]
    public void ParseText_Kilometers_RecognizesDistance()
    {
        const string text = "Służba 101\n123 km";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.AreEqual(123m, duties[0].DistanceKm);
        Assert.AreEqual(80, duties[0].Confidence.DistanceKm);
    }

    [TestMethod]
    public void ParseText_TimeFirstStop_RecognizesStop()
    {
        const string text = "Służba 101\n06:10 Katowice Dworzec";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba.pdf");

        Assert.AreEqual(1, duties[0].Stops.Count);
        Assert.AreEqual(1, duties[0].Stops[0].Sequence);
        Assert.AreEqual("Katowice Dworzec", duties[0].Stops[0].StopName);
        Assert.AreEqual(new TimeOnly(6, 10), duties[0].Stops[0].DepartureTime);
    }

    [TestMethod]
    public void ParseText_TransportDutySheet_RecognizesRealDutyFormat()
    {
        const string text = """
SŁUŻBA 60 WAŻNA OD 01.11.2025
Autobus 41 miejscowy
K-64 K-64 K-48
km przystanek 1 2 3
0 BAZA WPO 16:20
12 Lubin, ul. Paderewskiego obwodnica 16:35 16:36
18 ul. Kom.Eduk.Narodowej p.podziemne 16:44
21 ul. Leśna/Wyszyńskiego 16:50
56 ZG RUDNA ZACHODNIA 18:10 18:12
74 SZYB SG 18:40
218 BAZA WPO 03:05
DZIENNY PRZEBIEG: 218 km
ZATRUDNIENIE KIEROWCY
start pracy: 16:20
koniec pracy: 3:05
czas: 10:45 h
praca: 09:00
przer.śniad. 00:15
przerwa: 01:45
""";

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba-60.pdf");

        Assert.AreEqual(1, duties.Count);
        var duty = duties[0];
        Assert.AreEqual("60", duty.DutyNumber);
        Assert.AreEqual(new DateOnly(2025, 11, 1), duty.ValidFrom);
        Assert.AreEqual("Autobus 41 miejscowy", duty.VehicleRequirement);
        Assert.IsTrue(duty.Notes?.Contains("Ważna od 01.11.2025", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(new TimeOnly(16, 20), duty.StartTime);
        Assert.AreEqual(new TimeOnly(3, 5), duty.EndTime);
        Assert.AreEqual(540, duty.WorkMinutes);
        Assert.AreEqual(120, duty.BreakMinutes);
        Assert.AreEqual(218m, duty.DistanceKm);
        Assert.IsTrue(duty.Lines.Any(x => x.LineCode == "K-64"));
        Assert.IsTrue(duty.Lines.Any(x => x.LineCode == "K-48"));
        Assert.IsTrue(duty.Stops.Any(x => x.StopName == "BAZA WPO"));
        Assert.IsTrue(duty.Stops.Any(x => x.StopName == "ZG RUDNA ZACHODNIA"));
        Assert.IsTrue(duty.Stops.Any(x => x.StopName == "SZYB SG"));
        Assert.IsTrue(duty.Confidence.DutyNumber >= 90);
        Assert.IsTrue(duty.Confidence.StartTime >= 90);
        Assert.IsTrue(duty.Confidence.EndTime >= 90);
        Assert.IsTrue(duty.Confidence.DistanceKm >= 90);
    }}

