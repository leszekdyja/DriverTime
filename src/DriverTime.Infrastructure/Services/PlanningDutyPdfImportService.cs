using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;

namespace DriverTime.Infrastructure.Services;

public class PlanningDutyPdfImportService : IPlanningDutyPdfImportService
{
    private const string NoDutiesWarning = "Nie rozpoznano służb w pliku PDF. Sprawdź, czy plik zawiera tekst, a nie tylko skan.";

    public async Task<PlanningDutyPdfImportPreviewDto> PreviewAsync(
        string fileName,
        long fileSizeBytes,
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        string text;

        try
        {
            text = await PdfTextExtractor.ExtractTextAsync(pdfStream, cancellationToken);
        }
        catch
        {
            text = string.Empty;
            warnings.Add("Nie udało się odczytać tekstu z pliku PDF. Plik może być skanem albo mieć nietypową strukturę.");
        }

        var duties = ParseText(text, fileName, warnings);

        if (duties.Count == 0 && !warnings.Contains(NoDutiesWarning))
        {
            warnings.Add(NoDutiesWarning);
        }

        return new PlanningDutyPdfImportPreviewDto
        {
            FileName = fileName,
            FileSizeBytes = fileSizeBytes,
            DetectedDutyCount = duties.Count,
            Warnings = warnings.Distinct().ToList(),
            Duties = duties
        };
    }

    internal static List<PlanningDutyPdfImportPreviewItemDto> ParseText(
        string? text,
        string sourceFileName,
        ICollection<string>? warnings = null)
    {
        var normalizedText = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            warnings?.Add(NoDutiesWarning);
            return new List<PlanningDutyPdfImportPreviewItemDto>();
        }

        var dutyMatches = Regex.Matches(
            normalizedText,
            @"(?i)(?:służba|sluzba)\s*(?<number>[A-Za-z0-9\-/]+)");

        if (dutyMatches.Count == 0)
        {
            warnings?.Add(NoDutiesWarning);
            return new List<PlanningDutyPdfImportPreviewItemDto>();
        }

        var duties = new List<PlanningDutyPdfImportPreviewItemDto>();
        for (var index = 0; index < dutyMatches.Count; index++)
        {
            var match = dutyMatches[index];
            var nextStart = index + 1 < dutyMatches.Count
                ? dutyMatches[index + 1].Index
                : normalizedText.Length;
            var block = normalizedText[match.Index..nextStart];
            var dutyNumber = match.Groups["number"].Value.Trim();
            var times = ExtractTimes(block);

            var workMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+pracy|praca)\s*[:\-]?\s*(?<value>\d{1,2}:\d{2}|\d{1,4})");
            var breakMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+przerw|przerwy|przerwa)\s*[:\-]?\s*(?<value>\d{1,2}:\d{2}|\d{1,4})");
            var drivingMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+jazdy|jazda)\s*[:\-]?\s*(?<value>\d{1,2}:\d{2}|\d{1,4})");
            var distanceKm = ExtractDecimal(block, @"(?i)(?:km|kilometry|przebieg)\s*[:\-]?\s*(?<value>\d+(?:[,.]\d+)?)");
            var lines = ExtractLines(block);
            var stops = ExtractStops(block);

            duties.Add(new PlanningDutyPdfImportPreviewItemDto
            {
                DutyNumber = dutyNumber,
                Name = $"Służba {dutyNumber}",
                StartTime = times.Count > 0 ? times[0] : null,
                EndTime = times.Count > 1 ? times[1] : null,
                WorkMinutes = workMinutes,
                BreakMinutes = breakMinutes,
                DrivingMinutes = drivingMinutes,
                TotalDurationMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+całkowity|czas\s+calkowity|razem)\s*[:\-]?\s*(?<value>\d{1,2}:\d{2}|\d{1,4})"),
                DistanceKm = distanceKm,
                SourceFileName = sourceFileName,
                Lines = lines,
                Stops = stops,
                Confidence = new PlanningDutyPdfImportConfidenceDto
                {
                    DutyNumber = string.IsNullOrWhiteSpace(dutyNumber) ? 0 : 100,
                    StartTime = times.Count > 0 ? 90 : 0,
                    EndTime = times.Count > 1 ? 90 : 0,
                    Line = lines.Count > 0 ? 90 : 0,
                    Stops = stops.Count > 0 ? 75 : 0,
                    WorkingMinutes = workMinutes.HasValue ? 90 : 0,
                    DrivingMinutes = drivingMinutes.HasValue ? 90 : 0,
                    BreakMinutes = breakMinutes.HasValue ? 90 : 0,
                    DistanceKm = distanceKm.HasValue ? 75 : 0
                }
            });
        }

        return duties;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace('\u00a0', ' ');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\r\n|\r", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");

        return normalized.Trim();
    }

    private static List<TimeOnly> ExtractTimes(string text)
    {
        return Regex.Matches(text, @"(?<!\d)(?<hour>[0-2]?\d)[:.](?<minute>[0-5]\d)(?!\d)")
            .Select(match => TryParseTime(match.Value))
            .Where(time => time.HasValue)
            .Select(time => time!.Value)
            .Distinct()
            .ToList();
    }

    private static TimeOnly? TryParseTime(string value)
    {
        var normalized = value.Replace('.', ':');

        return TimeOnly.TryParseExact(normalized, new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : null;
    }

    private static int? ExtractMinutes(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value;
        if (value.Contains(':'))
        {
            var parts = value.Split(':');
            if (parts.Length == 2
                && int.TryParse(parts[0], out var hours)
                && int.TryParse(parts[1], out var minutes))
            {
                return Math.Max(0, hours * 60 + minutes);
            }
        }

        return int.TryParse(value, out var parsed) ? Math.Max(0, parsed) : null;
    }

    private static decimal? ExtractDecimal(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["value"].Value.Replace(',', '.');

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(0, parsed)
            : null;
    }

    private static List<PlanningDutyLineDto> ExtractLines(string text)
    {
        return Regex.Matches(text, @"(?i)\b(?<code>[A-Z]{1,3}-?\d{1,3}(?:bis)?)\b")
            .Select(match => match.Groups["code"].Value.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => new PlanningDutyLineDto
            {
                Id = Guid.NewGuid(),
                LineCode = code,
                Variant = code.EndsWith("bis", StringComparison.OrdinalIgnoreCase) ? "bis" : null
            })
            .ToList();
    }

    private static List<PlanningDutyStopDto> ExtractStops(string text)
    {
        var stops = new List<PlanningDutyStopDto>();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var match = Regex.Match(
                line,
                @"(?<time>[0-2]?\d[:.][0-5]\d)\s+(?<stop>[\p{L}0-9 .\-/]{3,80})");

            if (!match.Success)
            {
                continue;
            }

            stops.Add(new PlanningDutyStopDto
            {
                Id = Guid.NewGuid(),
                Sequence = stops.Count + 1,
                StopName = match.Groups["stop"].Value.Trim(),
                DepartureTime = TryParseTime(match.Groups["time"].Value)
            });
        }

        return stops;
    }

    private static class PdfTextExtractor
    {
        public static async Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await pdfStream.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();

            if (bytes.Length == 0)
            {
                return string.Empty;
            }

            var raw = Encoding.Latin1.GetString(bytes);
            var chunks = new List<string>();

            foreach (Match streamMatch in Regex.Matches(raw, @"(?s)(?<dict><<.*?>>)\s*stream\r?\n(?<data>.*?)\r?\nendstream"))
            {
                var dictionary = streamMatch.Groups["dict"].Value;
                var dataText = streamMatch.Groups["data"].Value;
                var data = Encoding.Latin1.GetBytes(dataText);

                if (dictionary.Contains("/FlateDecode", StringComparison.OrdinalIgnoreCase))
                {
                    data = TryInflate(data) ?? Array.Empty<byte>();
                }

                if (data.Length == 0)
                {
                    continue;
                }

                chunks.Add(ExtractPdfStrings(Encoding.Latin1.GetString(data)));
            }

            if (chunks.Count == 0)
            {
                chunks.Add(ExtractPdfStrings(raw));
            }

            return string.Join("\n", chunks.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static byte[]? TryInflate(byte[] data)
        {
            try
            {
                using var input = new MemoryStream(data);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                try
                {
                    using var input = new MemoryStream(data);
                    using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    deflate.CopyTo(output);
                    return output.ToArray();
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string ExtractPdfStrings(string content)
        {
            var values = new List<string>();
            foreach (Match match in Regex.Matches(content, @"\((?<text>(?:\\.|[^\\)])*)\)"))
            {
                var value = UnescapePdfString(match.Groups["text"].Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }

            if (values.Count > 0)
            {
                return string.Join("\n", values);
            }

            return Regex.Replace(content, @"[^\p{L}\p{N}:.,;\-/\s]", " ");
        }

        private static string UnescapePdfString(string value)
        {
            return value
                .Replace("\\(", "(")
                .Replace("\\)", ")")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }
    }
}


