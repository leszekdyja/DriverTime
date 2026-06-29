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
        Assert.AreEqual(90, duties[0].Confidence.DutyNumber);
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
        Assert.AreEqual(90, duties[0].Confidence.DutyNumber);
        Assert.AreEqual(0, duties[0].Confidence.StartTime);
        Assert.AreEqual(0, duties[0].Confidence.EndTime);
        Assert.AreEqual(0, duties[0].Confidence.Line);
        Assert.AreEqual(0, duties[0].Confidence.Stops);
        Assert.AreEqual(0, duties[0].Confidence.WorkingMinutes);
        Assert.AreEqual(0, duties[0].Confidence.DrivingMinutes);
        Assert.AreEqual(0, duties[0].Confidence.BreakMinutes);
        Assert.AreEqual(0, duties[0].Confidence.DistanceKm);
        Assert.IsTrue(warnings.Any(x => x.Contains("Nie rozpoznano pełnej tabeli przystanków", StringComparison.OrdinalIgnoreCase)));
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
    }
    [TestMethod]
    public void ParseText_Sluzba141Header_RecognizesDutyNumber()
    {
        const string text = "SŁUŻBA 141\nLinia K64\nod 2026.05.01";
        var warnings = new List<string>();

        var duties = PlanningDutyPdfImportService.ParseText(text, "sluzba-141.pdf", warnings);

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("141", duties[0].DutyNumber);
        Assert.AreEqual(new DateOnly(2026, 5, 1), duties[0].ValidFrom);
        Assert.IsTrue(duties[0].Lines.Any(x => x.LineCode == "K-64"));
    }

    [TestMethod]
    public void ParseText_FileNameSluzba141_RecognizesDutyNumberAndDate()
    {
        var warnings = new List<string>();

        var duties = PlanningDutyPdfImportService.ParseText(string.Empty, "Służba-141 św od 2026.05.01.pdf", warnings);

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("141", duties[0].DutyNumber);
        Assert.AreEqual(new DateOnly(2026, 5, 1), duties[0].ValidFrom);
        Assert.IsTrue(warnings.Any(x => x.Contains("Nie rozpoznano pełnej tabeli przystanków", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(warnings.Any(x => x.Contains("Nie rozpoznano służb", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ParseText_WithoutEmploymentHeadersButWithDutyNumber_ReturnsPartialDuty()
    {
        const string text = "Służba-141\nważna od 2026.05.01\nK48";
        var warnings = new List<string>();

        var duties = PlanningDutyPdfImportService.ParseText(text, "partial.pdf", warnings);

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("141", duties[0].DutyNumber);
        Assert.AreEqual(new DateOnly(2026, 5, 1), duties[0].ValidFrom);
        Assert.IsTrue(duties[0].Lines.Any(x => x.LineCode == "K-48"));
        Assert.IsTrue(warnings.Any(x => x.Contains("Nie rozpoznano pełnej tabeli przystanków", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ParseText_FileNameDateWithDayMonthYear_RecognizesValidFrom()
    {
        var duties = PlanningDutyPdfImportService.ParseText("Tekst nieczytelny", "Służba 141 od 01.05.2026.pdf");

        Assert.AreEqual(1, duties.Count);
        Assert.AreEqual("141", duties[0].DutyNumber);
        Assert.AreEqual(new DateOnly(2026, 5, 1), duties[0].ValidFrom);
    }

    [TestMethod]
    public void ParseText_TransportDutySheet141_ParsesSectionsWithoutMixingStopsTable()
    {
        const string text = """
SŁUŻBA 141 WAŻNA OD DNIA 01.05.2026
Autobus 41 miejscowy
K-58
Przystanki km
x6-7 święta
WPO LUBIN BAZA 0 20:42
Lubin, ul.Padarewskiego - obwodnica 6 20:52
ul. Kom.Eduk.Narodowej - p.podz. 8 20:55
ul. Leśna/Wyszyńskiego 9 20:57
ul. Szpakowa/Szkoła 10 20:59
ul. Piłsudskiego - Kaufland 11 21:01
Al. Niepodległości 13 21:04
ul. Jana Pawła II/Leszczynowa - bank 14 21:06
USTRONIE - pętla 6 21:09
ul. Jana Pawła II/Gwarków - PKS 16 21:10
ul. Hutnicza - pod kładką 17 21:12
ul. Wójta Henryka skrzyż. 18 21:14
POLKOWICE GŁÓWNE 33 21:25
Polkowice, ul. Chocianowska - sklep 37 21:32
21:35
POLKOWICE ZACHODNIE 39
21:45
SIEROSZOWICE SZYB SW III 46 21:55
SIEROSZOWICE SZYB SW III 0 22:05
POLKOWICE ZACHODNIE 7 22:20
Polkowice, ul. Chocianowska - sklep 9 22:23
POLKOWICE GŁÓWNE 13 22:30
Lubin, ul. Wójta Henryka skrzyż. 28 22:46
ul. Hutnicza - pod kładką 29 22:48
ul. Jana Pawła II/Gwarków - PKS 30 22:50
USTRONIE - pętla 30 22:51
ul. Jana Pawła II - kościół 32 22:54
Al. Niepodległości 33 22:56
ul. Kom.Eduk.Narodowej - p.podz. 35 22:59
ul. Leśna/Wyszyńskiego 36 23:01
ul. Szpakowa/Szkoła 37 23:03
ul. Piłsudskiego - Kaufland 38 23:05
ul.Padarewskiego - obwodnica 40 23:08
WPO LUBIN BAZA 46 23:18
DZIENNY PRZEBIEG: 92 km
ZATRUDNIENIE KIEROWCY: 12:35 23:35 11:00 h
praca 8 h
w tym: w tym: przer.śniad. 17:25 17:40 0:15 h
przerwa 17:40 20:40 2:00 h
Max czas pracy ogrzewania przy uwzględnieniu temperatury otoczenia 2 h
UWAGA:
Przy dowozie na PZ oczekiwać na linię K-173
""";

        var duties = PlanningDutyPdfImportService.ParseText(text, "Służba-141 św od 2026.05.01.pdf");

        Assert.AreEqual(1, duties.Count);
        var duty = duties[0];
        Assert.AreEqual("141", duty.DutyNumber);
        Assert.AreEqual(new DateOnly(2026, 5, 1), duty.ValidFrom);
        Assert.AreEqual("Autobus 41 miejscowy", duty.VehicleRequirement);
        Assert.AreEqual(new TimeOnly(12, 35), duty.StartTime);
        Assert.AreEqual(new TimeOnly(23, 35), duty.EndTime);
        Assert.AreEqual(660, duty.TotalDurationMinutes);
        Assert.AreEqual(480, duty.WorkMinutes);
        Assert.AreEqual(135, duty.BreakMinutes);
        Assert.AreEqual(92m, duty.DistanceKm);
        Assert.AreNotEqual(12m, duty.DistanceKm);
        Assert.AreEqual(1, duty.Lines.Count);
        Assert.AreEqual("K-58", duty.Lines[0].LineCode);
        Assert.IsFalse(duty.Lines.Any(x => x.LineCode is "4" or "29" or "H5"));
        Assert.IsTrue(duty.Stops.Count >= 20);
        Assert.IsTrue(duty.Stops.Any(x => x.StopName == "WPO LUBIN BAZA" && x.Km == 0m && x.DepartureTime == new TimeOnly(20, 42)));
        Assert.IsTrue(duty.Stops.Any(x => x.StopName == "POLKOWICE GŁÓWNE" && x.Km == 33m && x.DepartureTime == new TimeOnly(21, 25)));
        Assert.IsTrue(duty.Stops.Any(x => x.StopName == "WPO LUBIN BAZA" && x.Km == 46m && x.DepartureTime == new TimeOnly(23, 18)));
        Assert.AreEqual(100, duty.Confidence.Line);
        Assert.AreEqual(100, duty.Confidence.DistanceKm);
        Assert.AreEqual(100, duty.Confidence.StartTime);
        Assert.AreEqual(100, duty.Confidence.EndTime);
        Assert.AreEqual(100, duty.Confidence.WorkingMinutes);
        Assert.AreEqual(100, duty.Confidence.BreakMinutes);
    }
}
