using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Reports.DTOs;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverReportExportService : IDriverReportExportService
{
    private const string PdfContentType = "application/pdf";
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverReportExportService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<ReportExportDto?> ExportPdfAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(driverId, from, to, cancellationToken);

        return report is null
            ? null
            : new ReportExportDto
            {
                Content = GeneratePdf(report),
                ContentType = PdfContentType,
                FileName = GetFileName(report, "pdf")
            };
    }

    public async Task<ReportExportDto?> ExportExcelAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(driverId, from, to, cancellationToken);

        return report is null
            ? null
            : new ReportExportDto
            {
                Content = GenerateExcel(report),
                ContentType = ExcelContentType,
                FileName = GetFileName(report, "xlsx")
            };
    }

    private async Task<DriverReportDto?> GetReportAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var driver = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.CardNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (driver is null)
        {
            return null;
        }

        var company = await _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.Id == _currentUser.CompanyId)
            .Select(x => new
            {
                x.Name,
                x.VatNumber,
                x.Address,
                x.Email,
                x.Phone
            })
            .FirstAsync(cancellationToken);

        var fromUtc = DateTime.SpecifyKind(
            from.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);
        var toUtcExclusive = DateTime.SpecifyKind(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        var activities = await _dbContext.DriverActivities
            .AsNoTracking()
            .Where(x =>
                x.DddFile.CompanyId == _currentUser.CompanyId
                && x.DddFile.DriverId == driverId
                && x.StartUtc < toUtcExclusive
                && x.EndUtc > fromUtc)
            .OrderBy(x => x.StartUtc)
            .Select(x => new DriverReportActivitySource
            {
                Id = x.Id,
                DddFileId = x.DddFileId,
                DriverId = x.DddFile.DriverId,
                DriverCardNumber = x.DddFile.DriverCardNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                ActivityType = x.ActivityType
            })
            .ToListAsync(cancellationToken);

        var deduplicatedActivities = DeduplicateActivities(activities);
        var vehicleUses = await GetVehicleUsesAsync(deduplicatedActivities, cancellationToken);

        var report = new DriverReportDto
        {
            CompanyName = company.Name,
            CompanyVatNumber = company.VatNumber,
            CompanyAddress = company.Address,
            CompanyEmail = company.Email,
            CompanyPhone = company.Phone,
            DriverId = driver.Id,
            DriverFirstName = driver.FirstName,
            DriverLastName = driver.LastName,
            DriverCardNumber = driver.CardNumber,
            From = from,
            To = to,
            Activities = BuildReportActivities(
                deduplicatedActivities,
                vehicleUses,
                fromUtc,
                toUtcExclusive)
        };

        foreach (var activity in report.Activities)
        {
            AddDuration(report, activity.ActivityType, activity.DurationSeconds);
        }

        report.TotalDistanceKm = SumDistance(report.Activities);

        return report;
    }

    internal static List<DriverReportActivityDto> BuildReportActivities(
        IEnumerable<DriverReportActivitySource> activities,
        IReadOnlyCollection<VehicleUseReportSource> vehicleUses,
        DateTime fromUtc,
        DateTime toUtcExclusive)
    {
        var displayedVehicleUseIds = new HashSet<Guid>();

        return DeduplicateActivities(activities)
            .Select(activity =>
            {
                var start = activity.StartUtc < fromUtc ? fromUtc : activity.StartUtc;
                var end = activity.EndUtc > toUtcExclusive ? toUtcExclusive : activity.EndUtc;
                var vehicleUse = FindBestVehicleUse(activity, vehicleUses);
                var shouldShowOdometer = vehicleUse is not null
                    && displayedVehicleUseIds.Add(vehicleUse.Id);

                return new DriverReportActivityDto
                {
                    DddFileId = activity.DddFileId,
                    StartUtc = activity.StartUtc,
                    EndUtc = activity.EndUtc,
                    ActivityType = activity.ActivityType,
                    VehicleRegistration = ToReportVehicleDisplay(
                        vehicleUse?.RegistrationNumber ?? string.Empty),
                    DurationSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(start, end),
                    StartOdometerKm = shouldShowOdometer ? vehicleUse?.StartOdometerKm : null,
                    EndOdometerKm = shouldShowOdometer ? vehicleUse?.EndOdometerKm : null,
                    DistanceKm = shouldShowOdometer ? vehicleUse?.DistanceKm : null
                };
            })
            .ToList();
    }

    private async Task<List<VehicleUseReportSource>> GetVehicleUsesAsync(
        IReadOnlyCollection<DriverReportActivitySource> activities,
        CancellationToken cancellationToken)
    {
        if (activities.Count == 0)
        {
            return new List<VehicleUseReportSource>();
        }

        var dddFileIds = activities.Select(x => x.DddFileId).Distinct().ToList();
        return await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(x =>
                dddFileIds.Contains(x.DddFileId)
                && !string.IsNullOrWhiteSpace(x.RegistrationNumber))
            .Select(x => new VehicleUseReportSource
            {
                Id = x.Id,
                DddFileId = x.DddFileId,
                RegistrationNumber = x.RegistrationNumber,
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                StartOdometerKm = x.StartOdometerKm,
                EndOdometerKm = x.EndOdometerKm,
                DistanceKm = x.DistanceKm
            })
            .ToListAsync(cancellationToken);
    }

    private static List<DriverReportActivitySource> DeduplicateActivities(
        IEnumerable<DriverReportActivitySource> activities)
    {
        return activities
            .GroupBy(x => new
            {
                DriverKey = x.DriverId?.ToString("D") ?? x.DriverCardNumber,
                x.StartUtc,
                x.EndUtc,
                ActivityType = x.ActivityType.ToUpperInvariant()
            })
            .Select(x => x.OrderBy(activity => activity.Id).First())
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
    }

    internal static string FindVehicleRegistration(
        DriverReportActivitySource activity,
        IReadOnlyCollection<VehicleUseReportSource> vehicleUses)
    {
        return FindBestVehicleUse(activity, vehicleUses)?.RegistrationNumber.Trim() ?? string.Empty;
    }

    internal static VehicleUseReportSource? FindBestVehicleUse(
        DriverReportActivitySource activity,
        IReadOnlyCollection<VehicleUseReportSource> vehicleUses)
    {
        var candidates = vehicleUses
            .Where(x =>
                x.DddFileId == activity.DddFileId
                && x.StartUtc < activity.EndUtc
                && x.EndUtc > activity.StartUtc)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .Select(x => new
            {
                VehicleUse = x,
                IsContaining = x.StartUtc <= activity.StartUtc && x.EndUtc >= activity.EndUtc,
                OverlapSeconds = ActivityIntervalAggregationHelper.GetDurationSeconds(
                    x.StartUtc > activity.StartUtc ? x.StartUtc : activity.StartUtc,
                    x.EndUtc < activity.EndUtc ? x.EndUtc : activity.EndUtc)
            })
            .OrderByDescending(x => x.IsContaining)
            .ThenByDescending(x => x.OverlapSeconds)
            .Select(x => x.VehicleUse)
            .FirstOrDefault();
    }

    private static string ToReportVehicleDisplay(string registrationNumber) =>
        string.IsNullOrWhiteSpace(registrationNumber)
            ? "Brak danych"
            : registrationNumber.Trim();

    private static long GetDistanceSeconds(
        DriverReportActivitySource activity,
        VehicleUseReportSource vehicleUse)
    {
        if (vehicleUse.StartUtc < activity.EndUtc && vehicleUse.EndUtc > activity.StartUtc)
        {
            return 0;
        }

        var distance = vehicleUse.EndUtc <= activity.StartUtc
            ? activity.StartUtc - vehicleUse.EndUtc
            : vehicleUse.StartUtc - activity.EndUtc;

        return Math.Max(0, (long)Math.Ceiling(distance.TotalSeconds));
    }

    private static int? SumDistance(IEnumerable<DriverReportActivityDto> activities)
    {
        var distances = activities
            .Where(x => x.DistanceKm.HasValue)
            .Select(x => x.DistanceKm!.Value)
            .ToList();

        return distances.Count == 0 ? null : distances.Sum();
    }

    private static byte[] GeneratePdf(DriverReportDto report)
    {
        const int rowsPerPage = 16;
        var indexedActivities = report.Activities
            .Select((activity, index) => (Activity: activity, Index: index))
            .ToList();
        var activityPages = indexedActivities.Chunk(rowsPerPage).ToList();

        if (activityPages.Count == 0)
        {
            activityPages.Add(Array.Empty<(DriverReportActivityDto Activity, int Index)>());
        }

        var pageContents = new List<string>();

        for (var pageIndex = 0; pageIndex < activityPages.Count; pageIndex++)
        {
            pageContents.Add(BuildDriverReportPdfPage(
                report,
                activityPages[pageIndex],
                pageIndex + 1,
                activityPages.Count));
        }

        return BuildPdfDocument(pageContents);
    }

    private static byte[] BuildPdfDocument(IReadOnlyList<string> pageContents)
    {
        var objects = new List<byte[]>();
        var pageObjectNumbers = new List<int>();
        const int regularFontObjectNumber = 3;
        const int boldFontObjectNumber = 4;
        const int monoFontObjectNumber = 5;

        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Array.Empty<byte>());
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"));
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"));

        foreach (var content in pageContents)
        {
            var pageObjectNumber = objects.Count + 1;
            var contentObjectNumber = pageObjectNumber + 1;
            pageObjectNumbers.Add(pageObjectNumber);
            objects.Add(Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 {regularFontObjectNumber} 0 R /F2 {boldFontObjectNumber} 0 R /F3 {monoFontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));

            var contentBytes = Encoding.ASCII.GetBytes(content);
            var prefix = Encoding.ASCII.GetBytes($"<< /Length {contentBytes.Length} >>\nstream\n");
            var suffix = Encoding.ASCII.GetBytes("\nendstream");
            objects.Add(prefix.Concat(contentBytes).Concat(suffix).ToArray());
        }

        objects[1] = Encoding.ASCII.GetBytes(
            $"<< /Type /Pages /Count {pageObjectNumbers.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(x => $"{x} 0 R"))}] >>");

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{index + 1} 0 obj\n");
            stream.Write(objects[index]);
            WriteAscii(stream, "\nendobj\n");
        }

        var xrefPosition = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            WriteAscii(stream, $"{offset:0000000000} 00000 n \n");
        }

        WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefPosition}\n%%EOF");
        return stream.ToArray();
    }

    private static string BuildDriverReportPdfPage(
        DriverReportDto report,
        IReadOnlyList<(DriverReportActivityDto Activity, int Index)> activities,
        int pageNumber,
        int totalPages)
    {
        var page = new PdfPageBuilder();
        const double margin = 34;

        page.FillRectangle(0, 0, 842, 595, "#F8FAFC");
        page.FillRectangle(0, 526, 842, 69, "#0F172A");
        page.FillRectangle(0, 526, 842, 4, "#2563EB");
        page.Text(margin, 563, "DriverTime", 22, true, "#FFFFFF");
        page.Text(margin, 544, "Raport aktywnosci kierowcy", 11, false, "#CBD5E1");
        page.Text(808, 563, $"Zakres: {FormatDate(report.From)} - {FormatDate(report.To)}", 10, false, "#E2E8F0", PdfTextAlign.Right);
        page.Text(808, 545, $"Wygenerowano: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC", 9, false, "#CBD5E1", PdfTextAlign.Right);

        page.FillRectangle(margin, 434, 374, 72, "#FFFFFF");
        page.StrokeRectangle(margin, 434, 374, 72, "#E2E8F0");
        page.Text(50, 487, "Firma", 9, true, "#64748B");
        page.Text(50, 469, DisplayValue(report.CompanyName), 14, true, "#0F172A");
        page.Text(50, 451, $"NIP: {DisplayValue(report.CompanyVatNumber)}", 9, false, "#475569");
        page.Text(206, 451, $"Kontakt: {FormatCompanyContact(report)}", 9, false, "#475569");

        page.FillRectangle(434, 434, 374, 72, "#FFFFFF");
        page.StrokeRectangle(434, 434, 374, 72, "#E2E8F0");
        page.Text(450, 487, "Kierowca", 9, true, "#64748B");
        page.Text(450, 469, GetDriverName(report), 14, true, "#0F172A");
        page.Text(450, 451, $"Numer karty: {DisplayValue(report.DriverCardNumber)}", 9, false, "#475569");
        page.Text(634, 451, $"Okres: {FormatDate(report.From)} - {FormatDate(report.To)}", 9, false, "#475569");

        DrawSummaryCard(page, 34, "Jazda", FormatDuration(report.DrivingSeconds), "#2563EB");
        DrawSummaryCard(page, 189, "Praca", FormatDuration(report.WorkSeconds), "#0F766E");
        DrawSummaryCard(page, 344, "Odpoczynek", FormatDuration(report.RestSeconds), "#7C3AED");
        DrawSummaryCard(page, 499, "Dyspozycyjnosc", FormatDuration(report.AvailabilitySeconds), "#D97706");
        DrawSummaryCard(page, 654, "Kilometry", FormatDistance(report.TotalDistanceKm), "#334155");

        page.Text(margin, 345, "Aktywnosci", 13, true, "#0F172A");
        page.Text(808, 345, $"{report.Activities.Count} rekordow", 9, false, "#64748B", PdfTextAlign.Right);

        const double tableX = 34;
        const double headerY = 316;
        const double rowHeight = 17;
        page.FillRectangle(tableX, headerY, 774, 22, "#1E293B");
        page.Text(46, 323, "Lp.", 8, true, "#FFFFFF");
        page.Text(82, 323, "Data", 7, true, "#FFFFFF");
        page.Text(152, 323, "Od", 7, true, "#FFFFFF");
        page.Text(230, 323, "Do", 7, true, "#FFFFFF");
        page.Text(308, 323, "Aktywnosc", 7, true, "#FFFFFF");
        page.Text(396, 323, "Pojazd", 7, true, "#FFFFFF");
        page.Text(472, 323, "Czas", 7, true, "#FFFFFF");
        page.Text(548, 323, "Prz. pocz.", 7, true, "#FFFFFF");
        page.Text(630, 323, "Prz. kon.", 7, true, "#FFFFFF");
        page.Text(724, 323, "Km", 7, true, "#FFFFFF");

        if (report.Activities.Count == 0)
        {
            page.FillRectangle(tableX, 290, 774, 26, "#FFFFFF");
            page.StrokeRectangle(tableX, 290, 774, 26, "#E2E8F0");
            page.Text(46, 300, "Brak aktywnosci w wybranym okresie.", 9, false, "#64748B");
        }
        else
        {
            for (var rowIndex = 0; rowIndex < activities.Count; rowIndex++)
            {
                var row = activities[rowIndex];
                var y = headerY - ((rowIndex + 1) * rowHeight);
                page.FillRectangle(tableX, y, 774, rowHeight, rowIndex % 2 == 0 ? "#FFFFFF" : "#F1F5F9");
                page.StrokeRectangle(tableX, y, 774, rowHeight, "#E2E8F0");
                page.Text(46, y + 6, (row.Index + 1).ToString(CultureInfo.InvariantCulture), 8, false, "#334155", PdfTextAlign.Left, "F3");
                page.Text(82, y + 6, FormatDate(row.Activity.StartUtc), 7, false, "#334155", PdfTextAlign.Left, "F3");
                page.Text(152, y + 6, FormatTime(row.Activity.StartUtc), 7, false, "#334155", PdfTextAlign.Left, "F3");
                page.Text(230, y + 6, FormatTime(row.Activity.EndUtc), 7, false, "#334155", PdfTextAlign.Left, "F3");
                page.Text(308, y + 6, Truncate(GetActivityLabel(row.Activity.ActivityType), 16), 7, false, "#334155");
                page.Text(396, y + 6, Truncate(DisplayValue(row.Activity.VehicleRegistration), 13), 7, false, "#334155");
                page.Text(472, y + 6, FormatDuration(row.Activity.DurationSeconds), 7, false, "#334155");
                page.Text(548, y + 6, FormatNullableInt(row.Activity.StartOdometerKm), 7, false, "#334155", PdfTextAlign.Left, "F3");
                page.Text(630, y + 6, FormatNullableInt(row.Activity.EndOdometerKm), 7, false, "#334155", PdfTextAlign.Left, "F3");
                page.Text(724, y + 6, FormatNullableInt(row.Activity.DistanceKm), 7, false, "#334155", PdfTextAlign.Left, "F3");
            }
        }

        page.StrokeLine(margin, 38, 808, 38, "#CBD5E1", 0.6);
        page.Text(margin, 22, $"DriverTime - raport wygenerowany automatycznie dla {DisplayValue(report.CompanyName)}", 8, false, "#64748B");
        page.Text(808, 22, $"Strona {pageNumber}/{totalPages}", 8, false, "#64748B", PdfTextAlign.Right);

        return page.ToString();
    }

    private static void DrawSummaryCard(
        PdfPageBuilder page,
        double x,
        string label,
        string value,
        string accentColor)
    {
        page.FillRectangle(x, 374, 145, 42, "#FFFFFF");
        page.StrokeRectangle(x, 374, 145, 42, "#E2E8F0");
        page.FillRectangle(x, 374, 5, 42, accentColor);
        page.Text(x + 17, 399, label, 8, true, "#64748B");
        page.Text(x + 17, 383, value, 14, true, "#0F172A");
    }

    private static byte[] GenerateExcel(DriverReportDto report)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteZipEntry(archive, "[Content_Types].xml", ContentTypesXml);
            WriteZipEntry(archive, "_rels/.rels", RootRelationshipsXml);
            WriteZipEntry(archive, "xl/workbook.xml", WorkbookXml);
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml);
            WriteZipEntry(archive, "xl/styles.xml", StylesXml);
            WriteSummaryWorksheet(archive, report);
            WriteActivitiesWorksheet(archive, report);
        }

        return stream.ToArray();
    }

    private static void WriteSummaryWorksheet(ZipArchive archive, DriverReportDto report)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "3");
        writer.WriteAttributeString("topLeftCell", "A4");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cols");
        WriteColumn(writer, 1, 24);
        WriteColumn(writer, 2, 28);
        WriteColumn(writer, 3, 24);
        WriteColumn(writer, 4, 28);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");

        WriteStringRow(writer, 1, 1, "DriverTime");
        WriteStringRow(writer, 2, 2, "Raport aktywnosci kierowcy");
        WriteStringRow(writer, 4, 3, "Dane firmy");
        WriteKeyValueRow(writer, 5, "Nazwa firmy", DisplayValue(report.CompanyName));
        WriteKeyValueRow(writer, 6, "NIP", DisplayValue(report.CompanyVatNumber));
        WriteKeyValueRow(writer, 7, "Adres", DisplayValue(report.CompanyAddress));
        WriteKeyValueRow(writer, 8, "Email", DisplayValue(report.CompanyEmail));
        WriteKeyValueRow(writer, 9, "Telefon", DisplayValue(report.CompanyPhone));
        WriteStringRow(writer, 11, 3, "Dane kierowcy");
        WriteKeyValueRow(writer, 12, "Kierowca", GetDriverName(report));
        WriteKeyValueRow(writer, 13, "Numer karty", DisplayValue(report.DriverCardNumber));
        WriteKeyValueRow(writer, 14, "Zakres raportu", $"{FormatDate(report.From)} - {FormatDate(report.To)}");
        WriteKeyValueRow(writer, 15, "Wygenerowano", $"{DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC");
        WriteStringRow(writer, 17, 3, "Podsumowanie czasu");
        WriteStringRow(writer, 18, 6, "Jazda", "Praca", "Odpoczynek", "Dyspozycyjnosc", "Kilometry");
        WriteStringRow(writer, 19, 7,
            FormatDuration(report.DrivingSeconds),
            FormatDuration(report.WorkSeconds),
            FormatDuration(report.RestSeconds),
            FormatDuration(report.AvailabilitySeconds),
            FormatDistance(report.TotalDistanceKm));
        WriteKeyValueRow(writer, 21, "Liczba aktywnosci", report.Activities.Count.ToString(CultureInfo.InvariantCulture));

        writer.WriteEndElement();
        WriteMergedCells(writer, "A1:D1", "A2:D2", "A4:D4", "A11:D11", "A17:D17");
        writer.WriteStartElement("pageMargins");
        writer.WriteAttributeString("left", "0.5");
        writer.WriteAttributeString("right", "0.5");
        writer.WriteAttributeString("top", "0.6");
        writer.WriteAttributeString("bottom", "0.6");
        writer.WriteAttributeString("header", "0.3");
        writer.WriteAttributeString("footer", "0.3");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteActivitiesWorksheet(ZipArchive archive, DriverReportDto report)
    {
        var entry = archive.CreateEntry("xl/worksheets/sheet2.xml", CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false
        });

        var widths = GetActivityColumnWidths(report.Activities);

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane");
        writer.WriteAttributeString("ySplit", "1");
        writer.WriteAttributeString("topLeftCell", "A2");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cols");

        for (var index = 0; index < widths.Length; index++)
        {
            WriteColumn(writer, index + 1, widths[index]);
        }

        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");
        WriteStringRow(writer, 1, 8, "Lp.", "Data", "Od", "Do", "Aktywnosc", "Pojazd", "Czas trwania", "Przebieg poczatkowy", "Przebieg koncowy", "Km");

        for (var index = 0; index < report.Activities.Count; index++)
        {
            var activity = report.Activities[index];
            var row = index + 2;
            var style = index % 2 == 0 ? 9 : 10;

            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
            WriteNumberCell(writer, $"A{row}", index + 1, style);
            WriteDateOnlyCell(writer, $"B{row}", activity.StartUtc, 11);
            WriteDateCell(writer, $"C{row}", activity.StartUtc, 11);
            WriteDateCell(writer, $"D{row}", activity.EndUtc, 11);
            WriteStringCell(writer, $"E{row}", GetActivityLabel(activity.ActivityType), style);
            WriteStringCell(writer, $"F{row}", DisplayValue(activity.VehicleRegistration), style);
            WriteDurationCell(writer, $"G{row}", activity.DurationSeconds, 12);
            WriteNullableNumberCell(writer, $"H{row}", activity.StartOdometerKm, style);
            WriteNullableNumberCell(writer, $"I{row}", activity.EndOdometerKm, style);
            WriteNullableNumberCell(writer, $"J{row}", activity.DistanceKm, style);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A1:J{Math.Max(1, report.Activities.Count + 1)}");
        writer.WriteEndElement();
        writer.WriteStartElement("pageMargins");
        writer.WriteAttributeString("left", "0.4");
        writer.WriteAttributeString("right", "0.4");
        writer.WriteAttributeString("top", "0.5");
        writer.WriteAttributeString("bottom", "0.5");
        writer.WriteAttributeString("header", "0.3");
        writer.WriteAttributeString("footer", "0.3");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static double[] GetActivityColumnWidths(
        IReadOnlyCollection<DriverReportActivityDto> activities)
    {
        var activityWidth = activities.Count == 0
            ? 18
            : activities.Max(x => GetActivityLabel(x.ActivityType).Length) + 4;

        return new[]
        {
            8d,
            14d,
            18d,
            18d,
            Math.Clamp(activityWidth, 18, 34),
            21d,
            18d,
            18d,
            18d,
            12d
        };
    }

    private static void WriteMergedCells(XmlWriter writer, params string[] ranges)
    {
        writer.WriteStartElement("mergeCells");
        writer.WriteAttributeString("count", ranges.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var range in ranges)
        {
            writer.WriteStartElement("mergeCell");
            writer.WriteAttributeString("ref", range);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteColumn(XmlWriter writer, int index, double width)
    {
        writer.WriteStartElement("col");
        writer.WriteAttributeString("min", index.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", index.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", width.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void WriteKeyValueRow(XmlWriter writer, int row, string key, string value)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
        WriteStringCell(writer, $"A{row}", key, 4);
        WriteStringCell(writer, $"B{row}", value, 5);
        writer.WriteEndElement();
    }

    private static void WriteStringRow(
        XmlWriter writer,
        int rowNumber,
        int style,
        params string[] values)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString());

        for (var index = 0; index < values.Length; index++)
        {
            WriteStringCell(
                writer,
                $"{GetColumnName(index + 1)}{rowNumber}",
                values[index],
                style);
        }

        writer.WriteEndElement();
    }

    private static void WriteStringCell(
        XmlWriter writer,
        string reference,
        string value,
        int style)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("t", "inlineStr");
        writer.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
        writer.WriteStartElement("is");
        writer.WriteElementString("t", value);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteNumberCell(
        XmlWriter writer,
        string reference,
        double value,
        int style)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", reference);
        writer.WriteAttributeString("s", style.ToString(CultureInfo.InvariantCulture));
        writer.WriteElementString("v", value.ToString("0.###############", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteDateCell(
        XmlWriter writer,
        string reference,
        DateTime value,
        int style) =>
        WriteNumberCell(writer, reference, value.ToOADate(), style);

    private static void WriteDateOnlyCell(
        XmlWriter writer,
        string reference,
        DateTime value,
        int style) =>
        WriteNumberCell(writer, reference, value.Date.ToOADate(), style);

    private static void WriteNullableNumberCell(
        XmlWriter writer,
        string reference,
        int? value,
        int style)
    {
        if (value.HasValue)
        {
            WriteNumberCell(writer, reference, value.Value, style);
            return;
        }

        WriteStringCell(writer, reference, string.Empty, style);
    }

    private static void WriteDurationCell(
        XmlWriter writer,
        string reference,
        long seconds,
        int style) =>
        WriteNumberCell(writer, reference, Math.Max(seconds, 0) / 86400d, style);

    private static string GetColumnName(int index) => ((char)('A' + index - 1)).ToString();

    private static void WriteZipEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        stream.Write(Encoding.ASCII.GetBytes(value));
    }

    private static void AddDuration(
        DriverReportDto report,
        string activityType,
        long durationSeconds)
    {
        switch (activityType.ToUpperInvariant())
        {
            case "DRIVING": report.DrivingSeconds += durationSeconds; break;
            case "WORK": report.WorkSeconds += durationSeconds; break;
            case "REST": report.RestSeconds += durationSeconds; break;
            case "AVAILABILITY": report.AvailabilitySeconds += durationSeconds; break;
        }
    }

    private static string GetDriverName(DriverReportDto report)
    {
        var name = $"{report.DriverFirstName} {report.DriverLastName}".Trim();
        return DisplayValue(name);
    }

    private static string FormatCompanyContact(DriverReportDto report)
    {
        var values = new[] { report.CompanyEmail, report.CompanyPhone }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var contact = string.Join(" | ", values);
        return DisplayValue(contact);
    }

    private static string GetFileName(DriverReportDto report, string extension)
    {
        var driver = string.Join("-", new[] { report.DriverLastName, report.DriverFirstName }
            .Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();
        return $"drivertime-raport-{(driver.Length == 0 ? "kierowca" : driver)}-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.{extension}";
    }

    private static string GetActivityLabel(string value) => value.ToUpperInvariant() switch
    {
        "DRIVING" => "Jazda",
        "WORK" => "Praca",
        "REST" => "Odpoczynek",
        "AVAILABILITY" => "Dyspozycyjnosc",
        _ => value
    };

    private static string FormatDuration(long seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(seconds, 0));
        return $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
    }

    private static string FormatDate(DateOnly value) => value.ToString("dd.MM.yyyy");
    private static string FormatDate(DateTime value) => value.ToString("dd.MM.yyyy");
    private static string FormatTime(DateTime value) => value.ToString("HH:mm:ss");
    private static string FormatDateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm:ss");
    private static string FormatNullableInt(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    private static string FormatDistance(int? value) => value.HasValue ? $"{value.Value} km" : "Brak danych";
    private static string DisplayValue(string value) => string.IsNullOrWhiteSpace(value) ? "Brak danych" : value;
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static string ToPdfAscii(string value) => string.Concat(value.Select(ToPdfAscii));

    private static char ToPdfAscii(char character) => character switch
    {
        'ą' => 'a',
        'ć' => 'c',
        'ę' => 'e',
        'ł' => 'l',
        'ń' => 'n',
        'ó' => 'o',
        'ś' => 's',
        'ż' => 'z',
        'ź' => 'z',
        'Ą' => 'A',
        'Ć' => 'C',
        'Ę' => 'E',
        'Ł' => 'L',
        'Ń' => 'N',
        'Ó' => 'O',
        'Ś' => 'S',
        'Ż' => 'Z',
        'Ź' => 'Z',
        <= '\u007f' => character,
        _ => '?'
    };

    internal sealed class DriverReportActivitySource
    {
        public Guid Id { get; set; }

        public Guid DddFileId { get; set; }

        public Guid? DriverId { get; set; }

        public string DriverCardNumber { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public string ActivityType { get; set; } = string.Empty;
    }

    internal sealed class VehicleUseReportSource
    {
        public Guid Id { get; set; }

        public Guid DddFileId { get; set; }

        public string RegistrationNumber { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public int? StartOdometerKm { get; set; }

        public int? EndOdometerKm { get; set; }

        public int? DistanceKm { get; set; }
    }

    private enum PdfTextAlign
    {
        Left,
        Right
    }

    private sealed class PdfPageBuilder
    {
        private readonly StringBuilder _builder = new();

        public void FillRectangle(
            double x,
            double y,
            double width,
            double height,
            string color)
        {
            _builder
                .Append("q\n")
                .Append(ToRgb(color))
                .Append(" rg\n")
                .Append(FormatNumber(x))
                .Append(' ')
                .Append(FormatNumber(y))
                .Append(' ')
                .Append(FormatNumber(width))
                .Append(' ')
                .Append(FormatNumber(height))
                .Append(" re f\nQ\n");
        }

        public void StrokeRectangle(
            double x,
            double y,
            double width,
            double height,
            string color)
        {
            _builder
                .Append("q\n")
                .Append(ToRgb(color))
                .Append(" RG\n0.8 w\n")
                .Append(FormatNumber(x))
                .Append(' ')
                .Append(FormatNumber(y))
                .Append(' ')
                .Append(FormatNumber(width))
                .Append(' ')
                .Append(FormatNumber(height))
                .Append(" re S\nQ\n");
        }

        public void StrokeLine(
            double x1,
            double y1,
            double x2,
            double y2,
            string color,
            double width)
        {
            _builder
                .Append("q\n")
                .Append(ToRgb(color))
                .Append(" RG\n")
                .Append(FormatNumber(width))
                .Append(" w\n")
                .Append(FormatNumber(x1))
                .Append(' ')
                .Append(FormatNumber(y1))
                .Append(" m\n")
                .Append(FormatNumber(x2))
                .Append(' ')
                .Append(FormatNumber(y2))
                .Append(" l S\nQ\n");
        }

        public void Text(
            double x,
            double y,
            string text,
            double size,
            bool bold,
            string color,
            PdfTextAlign align = PdfTextAlign.Left,
            string? font = null)
        {
            var escapedText = EscapePdf(ToPdfAscii(text));
            var fontName = font ?? (bold ? "F2" : "F1");
            var safeX = align == PdfTextAlign.Right
                ? x - EstimateTextWidth(text, size, fontName)
                : x;

            _builder
                .Append("BT\n/")
                .Append(fontName)
                .Append(' ')
                .Append(FormatNumber(size))
                .Append(" Tf\n")
                .Append(ToRgb(color))
                .Append(" rg\n")
                .Append(FormatNumber(safeX))
                .Append(' ')
                .Append(FormatNumber(y))
                .Append(" Td\n(")
                .Append(escapedText)
                .Append(") Tj\nET\n");
        }

        public override string ToString() => _builder.ToString();

        private static string ToRgb(string color)
        {
            var value = color.TrimStart('#');
            var red = int.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            var green = int.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            var blue = int.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
            return $"{FormatNumber(red)} {FormatNumber(green)} {FormatNumber(blue)}";
        }

        private static double EstimateTextWidth(string text, double size, string font)
        {
            var factor = font == "F3" ? 0.6 : 0.52;
            return ToPdfAscii(text).Length * size * factor;
        }

        private static string FormatNumber(double value) =>
            value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;
    private const string RootRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;
    private const string WorkbookXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Podsumowanie" sheetId="1" r:id="rId1"/>
            <sheet name="Aktywno&#347;ci" sheetId="2" r:id="rId2"/>
          </sheets>
        </workbook>
        """;
    private const string WorkbookRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
          <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;
    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="2">
            <numFmt numFmtId="164" formatCode="dd.mm.yyyy hh:mm"/>
            <numFmt numFmtId="165" formatCode="[h]&quot; godz. &quot;mm&quot; min&quot;"/>
          </numFmts>
          <fonts count="5">
            <font><sz val="11"/><color rgb="FF334155"/><name val="Calibri"/></font>
            <font><b/><sz val="22"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FF334155"/><name val="Calibri"/></font>
            <font><b/><sz val="14"/><color rgb="FF0F172A"/><name val="Calibri"/></font>
          </fonts>
          <fills count="7">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF0F172A"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF2563EB"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFF8FAFC"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFEFF6FF"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFF1F5F9"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border/>
            <border><left style="thin"><color rgb="FFE2E8F0"/></left><right style="thin"><color rgb="FFE2E8F0"/></right><top style="thin"><color rgb="FFE2E8F0"/></top><bottom style="thin"><color rgb="FFE2E8F0"/></bottom></border>
          </borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="13">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="0" fontId="2" fillId="2" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="0" fontId="3" fillId="4" borderId="1" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="4" borderId="1" xfId="0" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
            <xf numFmtId="0" fontId="3" fillId="5" borderId="1" xfId="0" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="4" fillId="5" borderId="1" xfId="0" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="2" fillId="2" borderId="1" xfId="0" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="0" fontId="0" fillId="6" borderId="1" xfId="0" applyAlignment="1"><alignment vertical="center"/></xf>
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
            <xf numFmtId="165" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;
}
