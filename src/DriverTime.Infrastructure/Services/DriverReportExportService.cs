using System.IO.Compression;
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
            .Select(x => new DriverReportActivityDto
            {
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                ActivityType = x.ActivityType
            })
            .ToListAsync(cancellationToken);

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
            Activities = activities
        };

        foreach (var activity in activities)
        {
            var start = activity.StartUtc < fromUtc ? fromUtc : activity.StartUtc;
            var end = activity.EndUtc > toUtcExclusive ? toUtcExclusive : activity.EndUtc;
            activity.DurationSeconds = end > start ? (long)(end - start).TotalSeconds : 0;
            AddDuration(report, activity.ActivityType, activity.DurationSeconds);
        }

        return report;
    }

    private static byte[] GeneratePdf(DriverReportDto report)
    {
        const int rowsPerPage = 34;
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
            var lines = new List<string>
            {
                "DriverTime - Raport aktywnosci kierowcy",
                $"Firma: {DisplayValue(report.CompanyName)} | NIP: {DisplayValue(report.CompanyVatNumber)}",
                $"Adres: {DisplayValue(report.CompanyAddress)}",
                $"Kontakt: {FormatCompanyContact(report)}",
                $"Kierowca: {GetDriverName(report)}",
                $"Numer karty: {DisplayValue(report.DriverCardNumber)}",
                $"Zakres: {FormatDate(report.From)} - {FormatDate(report.To)}",
                $"Jazda: {FormatDuration(report.DrivingSeconds)} | Praca: {FormatDuration(report.WorkSeconds)} | Odpoczynek: {FormatDuration(report.RestSeconds)} | Dyspozycyjnosc: {FormatDuration(report.AvailabilitySeconds)}",
                string.Empty,
                "Lp.  Poczatek             Koniec               Typ                 Czas"
            };

            foreach (var row in activityPages[pageIndex])
            {
                lines.Add(string.Format(
                    "{0,-4} {1,-20} {2,-20} {3,-19} {4}",
                    row.Index + 1,
                    FormatDateTime(row.Activity.StartUtc),
                    FormatDateTime(row.Activity.EndUtc),
                    Truncate(GetActivityLabel(row.Activity.ActivityType), 19),
                    FormatDuration(row.Activity.DurationSeconds)));
            }

            if (report.Activities.Count == 0)
            {
                lines.Add("Brak aktywnosci w wybranym okresie.");
            }

            lines.Add(string.Empty);
            lines.Add($"Wygenerowano: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC | Strona {pageIndex + 1}/{activityPages.Count}");
            pageContents.Add(BuildPdfTextStream(lines));
        }

        return BuildPdfDocument(pageContents);
    }

    private static byte[] BuildPdfDocument(IReadOnlyList<string> pageContents)
    {
        var objects = new List<byte[]>();
        var pageObjectNumbers = new List<int>();
        var fontObjectNumber = 3;

        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"));
        objects.Add(Array.Empty<byte>());
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>"));

        foreach (var content in pageContents)
        {
            var pageObjectNumber = objects.Count + 1;
            var contentObjectNumber = pageObjectNumber + 1;
            pageObjectNumbers.Add(pageObjectNumber);
            objects.Add(Encoding.ASCII.GetBytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 {fontObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>"));

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

    private static string BuildPdfTextStream(IEnumerable<string> lines)
    {
        var builder = new StringBuilder("BT\n/F1 9 Tf\n11 TL\n35 555 Td\n");

        foreach (var line in lines)
        {
            builder.Append('(').Append(EscapePdf(ToPdfAscii(line))).Append(") Tj\nT*\n");
        }

        builder.Append("ET");
        return builder.ToString();
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
            WriteWorksheet(archive, report);
        }

        return stream.ToArray();
    }

    private static void WriteWorksheet(ZipArchive archive, DriverReportDto report)
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
        writer.WriteAttributeString("ySplit", "18");
        writer.WriteAttributeString("topLeftCell", "A19");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cols");
        WriteColumn(writer, 1, 8);
        WriteColumn(writer, 2, 22);
        WriteColumn(writer, 3, 22);
        WriteColumn(writer, 4, 22);
        WriteColumn(writer, 5, 20);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData");

        WriteRow(writer, 1, 2, "DriverTime");
        WriteRow(writer, 2, 1, "Raport aktywnosci kierowcy");
        WriteKeyValueRow(writer, 4, "Firma", DisplayValue(report.CompanyName));
        WriteKeyValueRow(writer, 5, "NIP", DisplayValue(report.CompanyVatNumber));
        WriteKeyValueRow(writer, 6, "Adres", DisplayValue(report.CompanyAddress));
        WriteKeyValueRow(writer, 7, "Email", DisplayValue(report.CompanyEmail));
        WriteKeyValueRow(writer, 8, "Telefon", DisplayValue(report.CompanyPhone));
        WriteKeyValueRow(writer, 10, "Kierowca", GetDriverName(report));
        WriteKeyValueRow(writer, 11, "Numer karty", DisplayValue(report.DriverCardNumber));
        WriteKeyValueRow(writer, 12, "Zakres dat", $"{FormatDate(report.From)} - {FormatDate(report.To)}");
        WriteKeyValueRow(writer, 13, "Wygenerowano", $"{DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC");
        WriteStringRow(writer, 15, 3, "Jazda", "Praca", "Odpoczynek", "Dyspozycyjnosc");
        WriteStringRow(writer, 16, 1,
            FormatDuration(report.DrivingSeconds),
            FormatDuration(report.WorkSeconds),
            FormatDuration(report.RestSeconds),
            FormatDuration(report.AvailabilitySeconds));
        WriteStringRow(writer, 18, 4, "Lp.", "Poczatek", "Koniec", "Typ", "Czas");

        for (var index = 0; index < report.Activities.Count; index++)
        {
            var activity = report.Activities[index];
            WriteStringRow(writer, index + 19, 0,
                (index + 1).ToString(),
                FormatDateTime(activity.StartUtc),
                FormatDateTime(activity.EndUtc),
                GetActivityLabel(activity.ActivityType),
                FormatDuration(activity.DurationSeconds));
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter");
        writer.WriteAttributeString("ref", $"A18:E{Math.Max(18, report.Activities.Count + 18)}");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteColumn(XmlWriter writer, int index, int width)
    {
        writer.WriteStartElement("col");
        writer.WriteAttributeString("min", index.ToString());
        writer.WriteAttributeString("max", index.ToString());
        writer.WriteAttributeString("width", width.ToString());
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void WriteKeyValueRow(XmlWriter writer, int row, string key, string value) =>
        WriteStringRow(writer, row, 0, key, value);

    private static void WriteRow(XmlWriter writer, int row, int style, string value) =>
        WriteStringRow(writer, row, style, value);

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
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", $"{GetColumnName(index + 1)}{rowNumber}");
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteAttributeString("s", style.ToString());
            writer.WriteStartElement("is");
            writer.WriteElementString("t", values[index]);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

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
    private static string FormatDateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm:ss");
    private static string DisplayValue(string value) => string.IsNullOrWhiteSpace(value) ? "Brak danych" : value;
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    private static string EscapePdf(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static string ToPdfAscii(string value) => string.Concat(value.Select(character => character <= 127 ? character : '?'));

    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ContentTypesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
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
          <sheets><sheet name="Raport kierowcy" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;
    private const string WorkbookRelationshipsXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;
    private const string StylesXml = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="3"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="14"/><color rgb="FF2563EB"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><name val="Calibri"/></font></fonts>
          <fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFEFF6FF"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1E3A8A"/></patternFill></fill></fills>
          <borders count="2"><border/><border><left style="thin"/><right style="thin"/><top style="thin"/><bottom style="thin"/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="5"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="0" fillId="2" borderId="1" xfId="0"/><xf numFmtId="0" fontId="2" fillId="3" borderId="1" xfId="0"/></cellXfs>
        </styleSheet>
        """;
}
