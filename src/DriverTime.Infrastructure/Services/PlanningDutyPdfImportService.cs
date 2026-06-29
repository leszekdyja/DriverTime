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

        if (IsTransportDutySheet(normalizedText))
        {
            var transportDuty = TryParseTransportDutySheet(normalizedText, sourceFileName);
            if (transportDuty is not null)
            {
                return new List<PlanningDutyPdfImportPreviewItemDto> { transportDuty };
            }
        }

        var dutyMatches = GetDutyMatches(normalizedText);
        if (dutyMatches.Count == 0)
        {
            warnings?.Add(NoDutiesWarning);
            return new List<PlanningDutyPdfImportPreviewItemDto>();
        }

        var duties = new List<PlanningDutyPdfImportPreviewItemDto>();
        for (var index = 0; index < dutyMatches.Count; index++)
        {
            var match = dutyMatches[index];
            var nextStart = index + 1 < dutyMatches.Count ? dutyMatches[index + 1].Index : normalizedText.Length;
            var block = normalizedText[match.Index..nextStart];
            var dutyNumber = match.Groups["number"].Value.Trim();
            var times = ExtractTimeRange(block);
            var workMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+pracy|praca|work)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
            var breakMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+przerw|przerwy|przerwa|break)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
            var drivingMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+jazdy|jazda|driving)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
            var distanceKm = ExtractDistance(block);
            var lines = ExtractLines(block);
            var stops = ExtractStops(block);

            duties.Add(new PlanningDutyPdfImportPreviewItemDto
            {
                DutyNumber = dutyNumber,
                Name = $"Służba {dutyNumber}",
                StartTime = times.Start,
                EndTime = times.End,
                WorkMinutes = workMinutes.Value,
                BreakMinutes = breakMinutes.Value,
                DrivingMinutes = drivingMinutes.Value,
                TotalDurationMinutes = ExtractMinutes(block, @"(?i)(?:czas\s+całkowity|czas\s+calkowity|razem)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})").Value,
                DistanceKm = distanceKm.Value,
                SourceFileName = sourceFileName,
                Lines = lines.Values,
                Stops = stops.Values,
                Confidence = new PlanningDutyPdfImportConfidenceDto
                {
                    DutyNumber = match.Value.Contains(':') || Regex.IsMatch(match.Value, @"(?i)(nr|duty|zadanie|kurs)") ? 100 : 80,
                    StartTime = times.Start.HasValue ? times.Confidence : 0,
                    EndTime = times.End.HasValue ? times.Confidence : 0,
                    Line = lines.Values.Count > 0 ? lines.Confidence : 0,
                    Stops = stops.Values.Count > 0 ? stops.Confidence : 0,
                    WorkingMinutes = workMinutes.Value.HasValue ? workMinutes.Confidence : 0,
                    DrivingMinutes = drivingMinutes.Value.HasValue ? drivingMinutes.Confidence : 0,
                    BreakMinutes = breakMinutes.Value.HasValue ? breakMinutes.Confidence : 0,
                    DistanceKm = distanceKm.Value.HasValue ? distanceKm.Confidence : 0
                }
            });
        }

        return duties;
    }

    private static bool IsTransportDutySheet(string text) =>
        text.Contains("ZATRUDNIENIE KIEROWCY", StringComparison.OrdinalIgnoreCase)
        && text.Contains("DZIENNY PRZEBIEG", StringComparison.OrdinalIgnoreCase);

    private static PlanningDutyPdfImportPreviewItemDto? TryParseTransportDutySheet(
        string text,
        string sourceFileName)
    {
        var dutyMatch = Regex.Match(text, @"(?im)\bSŁUŻBA\s+(?<number>[A-Za-z0-9\-/]+)");
        if (!dutyMatch.Success)
        {
            dutyMatch = Regex.Match(text, @"(?im)\bSLUZBA\s+(?<number>[A-Za-z0-9\-/]+)");
        }

        if (!dutyMatch.Success)
        {
            return null;
        }

        var employmentSection = ExtractSection(text, "ZATRUDNIENIE KIEROWCY");
        var validFrom = ExtractValidFrom(text);
        var vehicleRequirement = ExtractVehicleRequirement(text);
        var start = ExtractLabeledTime(employmentSection, @"(?i)(?:start\s+pracy|początek\s+pracy|poczatek\s+pracy|rozpoczęcie|rozpoczecie|start)");
        var end = ExtractLabeledTime(employmentSection, @"(?i)(?:koniec\s+pracy|zakończenie|zakonczenie|koniec)");
        var workMinutes = ExtractMinutes(employmentSection, @"(?i)\bpraca\b\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var breakfastBreak = ExtractMinutes(employmentSection, @"(?i)przer\.?\s*śniad\.?\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var mainBreak = ExtractMinutes(employmentSection, @"(?i)\bprzerwa\b\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var distance = ExtractDailyDistance(text);
        var lines = ExtractTransportLines(text);
        var stops = ExtractTransportStops(text, lines.Values.Select(x => x.LineCode).ToList());
        var notes = BuildTransportNotes(validFrom, vehicleRequirement);

        return new PlanningDutyPdfImportPreviewItemDto
        {
            DutyNumber = dutyMatch.Groups["number"].Value.Trim(),
            Name = $"Służba {dutyMatch.Groups["number"].Value.Trim()}",
            ValidFrom = validFrom,
            VehicleRequirement = vehicleRequirement,
            StartTime = start,
            EndTime = end,
            WorkMinutes = workMinutes.Value,
            BreakMinutes = SumNullableMinutes(mainBreak.Value, breakfastBreak.Value),
            DistanceKm = distance.Value,
            Notes = notes,
            SourceFileName = sourceFileName,
            Lines = lines.Values,
            Stops = stops.Values,
            Confidence = new PlanningDutyPdfImportConfidenceDto
            {
                DutyNumber = 100,
                StartTime = start.HasValue ? 100 : 0,
                EndTime = end.HasValue ? 100 : 0,
                Line = lines.Values.Count > 0 ? lines.Confidence : 0,
                Stops = stops.Values.Count > 0 ? stops.Confidence : 0,
                WorkingMinutes = workMinutes.Value.HasValue ? 100 : 0,
                BreakMinutes = mainBreak.Value.HasValue || breakfastBreak.Value.HasValue ? 100 : 0,
                DistanceKm = distance.Value.HasValue ? 100 : 0
            }
        };
    }

    private static string ExtractSection(string text, string heading)
    {
        var index = CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, heading, CompareOptions.IgnoreCase);
        return index < 0 ? string.Empty : text[index..];
    }

    private static DateOnly? ExtractValidFrom(string text)
    {
        var match = Regex.Match(text, @"(?i)WAŻNA\s+OD\s+(?<date>\d{1,2}[.]\d{1,2}[.]\d{4})");
        if (!match.Success)
        {
            match = Regex.Match(text, @"(?i)WAZNA\s+OD\s+(?<date>\d{1,2}[.]\d{1,2}[.]\d{4})");
        }

        return DateOnly.TryParseExact(match.Groups["date"].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static string? ExtractVehicleRequirement(string text)
    {
        var match = Regex.Match(text, @"(?im)^\s*(?<vehicle>Autobus\s+\d+\s+miejscowy)\s*$");
        return match.Success ? match.Groups["vehicle"].Value.Trim() : null;
    }

    private static TimeOnly? ExtractLabeledTime(string text, string labelPattern)
    {
        var match = Regex.Match(text, $@"{labelPattern}\s*[:\-]?\s*(?<time>\d{{1,2}}[:.]\d{{2}})");
        return match.Success ? TryParseTime(match.Groups["time"].Value) : null;
    }

    private static (decimal? Value, int Confidence) ExtractDailyDistance(string text)
    {
        var match = Regex.Match(text, @"(?i)DZIENNY\s+PRZEBIEG\s*[:\-]?\s*(?<value>\d+(?:[,.]\d+)?)\s*km");
        return match.Success && decimal.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? (value, 100)
            : (null, 0);
    }

    private static (List<PlanningDutyLineDto> Values, int Confidence) ExtractTransportLines(string text)
    {
        var codes = Regex.Matches(text, @"(?i)\bK-\d{1,4}(?:bis)?\b")
            .Select(x => x.Value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (codes.Select(code => new PlanningDutyLineDto
        {
            Id = Guid.NewGuid(),
            LineCode = code,
            Variant = code.EndsWith("bis", StringComparison.OrdinalIgnoreCase) ? "bis" : null
        }).ToList(), codes.Count > 0 ? 90 : 0);
    }

    private static (List<PlanningDutyStopDto> Values, int Confidence) ExtractTransportStops(
        string text,
        IReadOnlyCollection<string> lineCodes)
    {
        var stops = new List<PlanningDutyStopDto>();
        var knownHeadings = new[]
        {
            "SŁUŻBA", "SLUZBA", "WAŻNA", "WAZNA", "Autobus", "DZIENNY", "ZATRUDNIENIE", "KIEROWCY", "czas", "praca", "przerwa", "przer.śniad"
        };

        foreach (var rawLine in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (line.Length < 3 || knownHeadings.Any(x => line.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (lineCodes.Any(code => string.Equals(code, line, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var match = Regex.Match(line, @"^(?:(?<km>\d+(?:[,.]\d+)?)\s+)?(?<stop>[\p{L}0-9,. /\-]+?)(?:\s+(?<times>(?:\d{1,2}[:.]\d{2}\s*)+))?$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var stopName = Regex.Replace(match.Groups["stop"].Value.Trim(), @"\s+", " ");
            if (!IsLikelyTransportStop(stopName))
            {
                continue;
            }

            decimal? km = null;
            if (decimal.TryParse(match.Groups["km"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedKm))
            {
                km = parsedKm;
            }

            var times = Regex.Matches(match.Groups["times"].Value, @"\d{1,2}[:.]\d{2}")
                .Select(x => TryParseTime(x.Value))
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            stops.Add(new PlanningDutyStopDto
            {
                Id = Guid.NewGuid(),
                Sequence = stops.Count + 1,
                StopName = stopName,
                Km = km,
                ArrivalTime = times.Count > 1 ? times[0] : null,
                DepartureTime = times.Count > 0 ? times[^1] : null,
                LineCode = lineCodes.Count == 1 ? lineCodes.First() : null
            });
        }

        return (stops, stops.Count > 0 ? 80 : 0);
    }

    private static bool IsLikelyTransportStop(string value)
    {
        if (Regex.IsMatch(value, @"^K-\d", RegexOptions.IgnoreCase)) return false;
        if (Regex.IsMatch(value, @"^\d+(?:[,.]\d+)?$")) return false;
        if (Regex.IsMatch(value, @"^\d{1,2}[:.]\d{2}")) return false;
        return value.Contains(' ')
            || value.Contains(',')
            || value.Equals("BAZA WPO", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ZG ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("SZYB ", StringComparison.OrdinalIgnoreCase);
    }

    private static int? SumNullableMinutes(params int?[] values)
    {
        var present = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        return present.Count == 0 ? null : present.Sum();
    }

    private static string? BuildTransportNotes(DateOnly? validFrom, string? vehicleRequirement)
    {
        var notes = new List<string>();
        if (validFrom.HasValue) notes.Add($"Ważna od {validFrom:dd.MM.yyyy}");
        if (!string.IsNullOrWhiteSpace(vehicleRequirement)) notes.Add(vehicleRequirement);
        return notes.Count == 0 ? null : string.Join("; ", notes);
    }
    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Replace('\u00a0', ' ');
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\r\n|\r", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static List<Match> GetDutyMatches(string text) => Regex.Matches(
            text,
            @"(?im)\b(?:(?:służba|sluzba)\s*(?:nr\s*)?|nr\s+służby\s*:\s*|nr\s+sluzby\s*:\s*|duty\s+|zadanie\s+|kurs\s+)(?<number>[A-Za-z0-9\-/]+)")
        .Cast<Match>()
        .OrderBy(x => x.Index)
        .ToList();

    private static (TimeOnly? Start, TimeOnly? End, int Confidence) ExtractTimeRange(string text)
    {
        var labeledPatterns = new[]
        {
            @"(?i)od\s+(?<start>\d{1,2}[:.]\d{2})\s+do\s+(?<end>\d{1,2}[:.]\d{2})",
            @"(?i)start\s*(?<start>\d{1,2}[:.]\d{2}).*?koniec\s*(?<end>\d{1,2}[:.]\d{2})",
            @"(?i)rozpoczęcie\s*(?<start>\d{1,2}[:.]\d{2}).*?zakończenie\s*(?<end>\d{1,2}[:.]\d{2})"
        };

        foreach (var pattern in labeledPatterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success)
            {
                return (TryParseTime(match.Groups["start"].Value), TryParseTime(match.Groups["end"].Value), 100);
            }
        }

        var range = Regex.Match(text, @"(?<!\d)(?<start>\d{1,2}[:.]\d{2})\s*[-–—]\s*(?<end>\d{1,2}[:.]\d{2})(?!\d)");
        if (range.Success)
        {
            return (TryParseTime(range.Groups["start"].Value), TryParseTime(range.Groups["end"].Value), 80);
        }

        var times = Regex.Matches(text, @"(?<!\d)(?<time>\d{1,2}[:.]\d{2})(?!\d)")
            .Select(x => TryParseTime(x.Groups["time"].Value))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        return (times.Count > 0 ? times[0] : null, times.Count > 1 ? times[1] : null, times.Count > 0 ? 50 : 0);
    }

    private static TimeOnly? TryParseTime(string value)
    {
        var normalized = value.Replace('.', ':');
        return TimeOnly.TryParseExact(normalized, new[] { "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
    }

    private static (int? Value, int Confidence) ExtractMinutes(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        if (!match.Success) return (null, 0);
        return (ParseMinutes(match.Groups["value"].Value), 100);
    }

    private static int? ParseMinutes(string raw)
    {
        var value = raw.Trim().ToLowerInvariant();
        var hMatch = Regex.Match(value, @"(?<h>\d{1,2})\s*h\s*(?<m>\d{1,2})(?:\s*min)?");
        if (hMatch.Success) return int.Parse(hMatch.Groups["h"].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(hMatch.Groups["m"].Value, CultureInfo.InvariantCulture);
        if (value.Contains(':'))
        {
            var parts = value.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes)) return hours * 60 + minutes;
        }
        value = value.Replace("min", string.Empty).Trim();
        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static (decimal? Value, int Confidence) ExtractDistance(string text)
    {
        var patterns = new[]
        {
            @"(?i)(?:dystans|odległość|odleglosc|distance|km)\s*[:\-]?\s*(?<value>\d+(?:[,.]\d+)?)",
            @"(?i)(?<value>\d+(?:[,.]\d+)?)\s*km\b"
        };
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern);
            if (match.Success && decimal.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return (Math.Max(0, parsed), pattern.Contains("?:dystans") ? 100 : 80);
            }
        }
        return (null, 0);
    }

    private static (List<PlanningDutyLineDto> Values, int Confidence) ExtractLines(string text)
    {
        var labeled = Regex.Matches(text, @"(?i)\b(?:linia|line|l|trasa)\s*[:\-]?\s*(?<code>[A-Z]{0,3}-?\d{1,4}(?:bis)?)\b")
            .Select(x => x.Groups["code"].Value.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var codes = labeled.Count > 0 ? labeled : Regex.Matches(text, @"(?i)\b(?<code>[A-Z]{1,3}-\d{1,4}(?:bis)?)\b")
            .Select(x => x.Groups["code"].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (codes.Select(code => new PlanningDutyLineDto
        {
            Id = Guid.NewGuid(),
            LineCode = code,
            Variant = code.EndsWith("bis", StringComparison.OrdinalIgnoreCase) ? "bis" : null
        }).ToList(), labeled.Count > 0 ? 100 : codes.Count > 0 ? 50 : 0);
    }

    private static (List<PlanningDutyStopDto> Values, int Confidence) ExtractStops(string text)
    {
        var stops = new List<PlanningDutyStopDto>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Regex.IsMatch(line, @"^\d{1,2}[:.]\d{2}\s*[-–—]\s*\d{1,2}[:.]\d{2}$"))
            {
                continue;
            }

            Match match = Regex.Match(line, @"^(?<time>\d{1,2}[:.]\d{2})\s+(?:(?<kind>przyj\.|odj\.)\s+)?(?<stop>[\p{L}0-9 .\-/]{3,80})$");
            TimeOnly? arrival = null;
            TimeOnly? departure = null;
            string? stopName = null;

            if (match.Success)
            {
                stopName = match.Groups["stop"].Value.Trim();
                if (match.Groups["kind"].Value.StartsWith("przyj", StringComparison.OrdinalIgnoreCase)) arrival = TryParseTime(match.Groups["time"].Value);
                else departure = TryParseTime(match.Groups["time"].Value);
            }
            else
            {
                match = Regex.Match(line, @"^(?<stop>[\p{L}0-9 .\-/]{3,80})\s+(?<time>\d{1,2}[:.]\d{2})$");
                if (match.Success)
                {
                    stopName = match.Groups["stop"].Value.Trim();
                    departure = TryParseTime(match.Groups["time"].Value);
                }
            }

            if (string.IsNullOrWhiteSpace(stopName)) continue;
            stops.Add(new PlanningDutyStopDto
            {
                Id = Guid.NewGuid(),
                Sequence = stops.Count + 1,
                StopName = stopName,
                ArrivalTime = arrival,
                DepartureTime = departure
            });
        }
        return (stops, stops.Count > 0 ? 80 : 0);
    }

    private static class PdfTextExtractor
    {
        public static async Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await pdfStream.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            if (bytes.Length == 0) return string.Empty;
            var raw = Encoding.Latin1.GetString(bytes);
            var chunks = new List<string>();
            foreach (Match streamMatch in Regex.Matches(raw, @"(?s)(?<dict><<.*?>>)\s*stream\r?\n(?<data>.*?)\r?\nendstream"))
            {
                var dictionary = streamMatch.Groups["dict"].Value;
                var data = Encoding.Latin1.GetBytes(streamMatch.Groups["data"].Value);
                if (dictionary.Contains("/FlateDecode", StringComparison.OrdinalIgnoreCase)) data = TryInflate(data) ?? Array.Empty<byte>();
                if (data.Length > 0) chunks.Add(ExtractPdfStrings(Encoding.Latin1.GetString(data)));
            }
            if (chunks.Count == 0) chunks.Add(ExtractPdfStrings(raw));
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
                catch { return null; }
            }
        }

        private static string ExtractPdfStrings(string content)
        {
            var values = new List<string>();
            foreach (Match match in Regex.Matches(content, @"\((?<text>(?:\\.|[^\\)])*)\)"))
            {
                var value = UnescapePdfString(match.Groups["text"].Value);
                if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
            }
            return values.Count > 0 ? string.Join("\n", values) : Regex.Replace(content, @"[^\p{L}\p{N}:.,;\-/\s]", " ");
        }

        private static string UnescapePdfString(string value) => value
            .Replace("\\(", "(")
            .Replace("\\)", ")")
            .Replace("\\n", "\n")
            .Replace("\\r", "\n")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");
    }
}




