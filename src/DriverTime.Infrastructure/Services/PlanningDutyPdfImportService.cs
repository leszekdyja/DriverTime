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
        var searchText = string.IsNullOrWhiteSpace(normalizedText)
            ? sourceFileName
            : $"{normalizedText}\n{sourceFileName}";

        if (IsTransportDutySheet(normalizedText))
        {
            var transportDuty = TryParseTransportDutySheet(normalizedText, sourceFileName);
            if (transportDuty is not null)
            {
                if (transportDuty.Stops.Count == 0)
                {
                    warnings?.Add("Nie rozpoznano pełnej tabeli przystanków.");
                }

                return new List<PlanningDutyPdfImportPreviewItemDto> { transportDuty };
            }
        }

        var dutyMatches = string.IsNullOrWhiteSpace(normalizedText)
            ? new List<Match>()
            : GetDutyMatches(normalizedText);

        if (dutyMatches.Count > 0)
        {
            var duties = new List<PlanningDutyPdfImportPreviewItemDto>();
            for (var index = 0; index < dutyMatches.Count; index++)
            {
                var match = dutyMatches[index];
                var nextStart = index + 1 < dutyMatches.Count ? dutyMatches[index + 1].Index : normalizedText.Length;
                var block = normalizedText[match.Index..nextStart];
                var duty = CreateGenericDutyPreview(
                    match.Groups["number"].Value.Trim(),
                    block,
                    sourceFileName,
                    ExtractValidFrom(block) ?? ExtractValidFrom(sourceFileName),
                    dutyNumberConfidence: match.Value.Contains(':') || Regex.IsMatch(match.Value, @"(?i)(nr|duty|zadanie|kurs)") ? 100 : 90);

                if (duty.Stops.Count == 0)
                {
                    warnings?.Add("Nie rozpoznano pełnej tabeli przystanków.");
                }

                duties.Add(duty);
            }

            return duties;
        }

        var fileDutyNumber = ExtractDutyNumber(searchText);
        if (!string.IsNullOrWhiteSpace(fileDutyNumber))
        {
            var duty = CreateGenericDutyPreview(
                fileDutyNumber,
                searchText,
                sourceFileName,
                ExtractValidFrom(searchText),
                dutyNumberConfidence: normalizedText.Contains(fileDutyNumber, StringComparison.OrdinalIgnoreCase) ? 70 : 60);

            if (duty.Stops.Count == 0)
            {
                warnings?.Add("Nie rozpoznano pełnej tabeli przystanków.");
            }

            return new List<PlanningDutyPdfImportPreviewItemDto> { duty };
        }

        warnings?.Add(NoDutiesWarning);
        return new List<PlanningDutyPdfImportPreviewItemDto>();
    }

    private static PlanningDutyPdfImportPreviewItemDto CreateGenericDutyPreview(
        string dutyNumber,
        string text,
        string sourceFileName,
        DateOnly? validFrom,
        int dutyNumberConfidence)
    {
        var times = ExtractTimeRange(text);
        var workMinutes = ExtractMinutes(text, @"(?i)(?:czas\s+pracy|praca|work)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var breakMinutes = ExtractMinutes(text, @"(?i)(?:czas\s+przerw|przerwy|przerwa|break)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var drivingMinutes = ExtractMinutes(text, @"(?i)(?:czas\s+jazdy|jazda|driving)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var totalMinutes = ExtractMinutes(text, @"(?i)(?:czas\s+całkowity|czas\s+calkowity|razem)\s*[:\-]?\s*(?<value>\d{1,2}\s*h\s*\d{1,2}(?:\s*min)?|\d{1,2}:\d{2}|\d{1,4}\s*min|\d{1,4})");
        var distanceKm = ExtractDailyDistance(text);
        if (!distanceKm.Value.HasValue)
        {
            distanceKm = ExtractDistance(text);
        }

        var lines = ExtractLines(text);
        if (lines.Values.Count == 0)
        {
            lines = ExtractTransportLines(text);
        }

        var stops = ExtractTransportStops(text, lines.Values.Select(x => x.LineCode).ToList());
        if (stops.Values.Count == 0)
        {
            stops = ExtractStops(text);
        }

        var vehicleRequirement = ExtractVehicleRequirement(text);
        var notes = BuildTransportNotes(validFrom, vehicleRequirement);

        return new PlanningDutyPdfImportPreviewItemDto
        {
            DutyNumber = dutyNumber.Trim(),
            Name = $"Służba {dutyNumber.Trim()}",
            ValidFrom = validFrom,
            VehicleRequirement = vehicleRequirement,
            StartTime = times.Start,
            EndTime = times.End,
            WorkMinutes = workMinutes.Value,
            BreakMinutes = breakMinutes.Value,
            DrivingMinutes = drivingMinutes.Value,
            TotalDurationMinutes = totalMinutes.Value,
            DistanceKm = distanceKm.Value,
            Notes = notes,
            SourceFileName = sourceFileName,
            Lines = lines.Values,
            Stops = stops.Values,
            Confidence = new PlanningDutyPdfImportConfidenceDto
            {
                DutyNumber = dutyNumberConfidence,
                StartTime = times.Start.HasValue ? times.Confidence : 0,
                EndTime = times.End.HasValue ? times.Confidence : 0,
                Line = lines.Values.Count > 0 ? lines.Confidence : 0,
                Stops = stops.Values.Count > 0 ? stops.Confidence : 0,
                WorkingMinutes = workMinutes.Value.HasValue ? workMinutes.Confidence : 0,
                DrivingMinutes = drivingMinutes.Value.HasValue ? drivingMinutes.Confidence : 0,
                BreakMinutes = breakMinutes.Value.HasValue ? breakMinutes.Confidence : 0,
                DistanceKm = distanceKm.Value.HasValue ? distanceKm.Confidence : 0
            }
        };
    }

    private static string? ExtractDutyNumber(string text)
    {
        var match = GetDutyMatches(text).FirstOrDefault();
        return match?.Groups["number"].Value.Trim();
    }
    private static bool IsTransportDutySheet(string text) =>
        text.Contains("ZATRUDNIENIE KIEROWCY", StringComparison.OrdinalIgnoreCase)
        && text.Contains("DZIENNY PRZEBIEG", StringComparison.OrdinalIgnoreCase);

    private static PlanningDutyPdfImportPreviewItemDto? TryParseTransportDutySheet(
        string text,
        string sourceFileName)
    {
        var sections = SplitTransportDutySections(text);
        var dutyNumber = ExtractDutyNumber(sections.Header) ?? ExtractDutyNumber(sourceFileName);
        if (string.IsNullOrWhiteSpace(dutyNumber))
        {
            return null;
        }

        var validFrom = ExtractValidFrom(sections.Header) ?? ExtractValidFrom(sourceFileName);
        var vehicleRequirement = ExtractVehicleRequirement(sections.Header);
        var line = ExtractTransportLines(sections.Header);
        var distance = ExtractDailyDistance(sections.DailyDistance);
        var start = ExtractEmploymentStart(sections.DriverEmployment);
        var end = ExtractEmploymentEnd(sections.DriverEmployment);
        var totalMinutes = ExtractEmploymentTotalDuration(sections.DriverEmployment);
        var workMinutes = ExtractEmploymentDuration(sections.DriverEmployment, "praca");
        var breakfastBreak = ExtractEmploymentDuration(sections.DriverEmployment, "przer.śniad", "przer śniad", "przerwa śniadaniowa");
        var mainBreak = ExtractEmploymentDuration(sections.DriverEmployment, "przerwa");
        var stops = ExtractTransportStops(sections.StopsTable, line.Values.Select(x => x.LineCode).ToList());
        var notes = BuildTransportNotes(validFrom, vehicleRequirement);

        return new PlanningDutyPdfImportPreviewItemDto
        {
            DutyNumber = dutyNumber.Trim(),
            Name = $"Służba {dutyNumber.Trim()}",
            ValidFrom = validFrom,
            VehicleRequirement = vehicleRequirement,
            StartTime = start,
            EndTime = end,
            TotalDurationMinutes = totalMinutes.Value,
            WorkMinutes = workMinutes.Value,
            BreakMinutes = SumNullableMinutes(mainBreak.Value, breakfastBreak.Value),
            DistanceKm = distance.Value,
            Notes = notes,
            SourceFileName = sourceFileName,
            Lines = line.Values,
            Stops = stops.Values,
            Confidence = new PlanningDutyPdfImportConfidenceDto
            {
                DutyNumber = ExtractDutyNumber(sections.Header) is not null ? 100 : 60,
                StartTime = start.HasValue ? 100 : 0,
                EndTime = end.HasValue ? 100 : 0,
                Line = line.Values.Count > 0 ? 100 : 0,
                Stops = stops.Values.Count > 0 ? stops.Confidence : 0,
                WorkingMinutes = workMinutes.Value.HasValue ? 100 : 0,
                BreakMinutes = mainBreak.Value.HasValue || breakfastBreak.Value.HasValue ? 100 : 0,
                DistanceKm = distance.Value.HasValue ? 100 : 0
            }
        };
    }

    private sealed record TransportDutySections(
        string Header,
        string StopsTable,
        string DailyDistance,
        string DriverEmployment,
        string Notes);

    private static TransportDutySections SplitTransportDutySections(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var dailyIndex = FindLineIndex(lines, @"(?i)DZIENNY\s+PRZEBIEG");
        var employmentIndex = FindLineIndex(lines, @"(?i)ZATRUDNIENIE\s+KIEROWCY");
        var firstStopIndex = FindFirstStopTableLineIndex(lines);
        var headerEnd = new[] { firstStopIndex, dailyIndex, employmentIndex }
            .Where(x => x >= 0)
            .DefaultIfEmpty(lines.Count)
            .Min();
        var stopsEnd = new[] { dailyIndex, employmentIndex }
            .Where(x => x >= 0 && (firstStopIndex < 0 || x > firstStopIndex))
            .DefaultIfEmpty(lines.Count)
            .Min();
        var dailyEnd = employmentIndex >= 0 && employmentIndex > dailyIndex ? employmentIndex : lines.Count;

        return new TransportDutySections(
            JoinLines(lines, 0, headerEnd),
            firstStopIndex >= 0 ? JoinLines(lines, firstStopIndex, stopsEnd) : string.Empty,
            dailyIndex >= 0 ? JoinLines(lines, dailyIndex, dailyEnd) : string.Empty,
            employmentIndex >= 0 ? JoinLines(lines, employmentIndex, lines.Count) : string.Empty,
            string.Empty);
    }

    private static int FindLineIndex(IReadOnlyList<string> lines, string pattern)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (Regex.IsMatch(lines[index], pattern)) return index;
        }

        return -1;
    }

    private static int FindFirstStopTableLineIndex(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (Regex.IsMatch(line, @"^\s*\d+(?:[,.]\d+)?\s+[\p{L}0-9,. /\-]+\s+\d{1,2}[:.]\d{2}") || Regex.IsMatch(line, @"^[\p{L}0-9,. /\-]+\s+\d+(?:[,.]\d+)?\s+\d{1,2}[:.]\d{2}"))
            {
                return index;
            }
        }

        return -1;
    }

    private static string JoinLines(IReadOnlyList<string> lines, int start, int end) =>
        start >= end ? string.Empty : string.Join("\n", lines.Skip(start).Take(end - start));

    private static TimeOnly? ExtractEmploymentStart(string text)
    {
        return ExtractLabeledTime(text, @"(?i)(?:start\s+pracy|początek\s+pracy|poczatek\s+pracy|rozpoczęcie|rozpoczecie|start)")
            ?? ExtractFirstTimeFromEmploymentLine(text, "zatrudnienie", occurrence: 0);
    }

    private static TimeOnly? ExtractEmploymentEnd(string text)
    {
        return ExtractLabeledTime(text, @"(?i)(?:koniec\s+pracy|zakończenie|zakonczenie|koniec)")
            ?? ExtractFirstTimeFromEmploymentLine(text, "zatrudnienie", occurrence: 1);
    }

    private static TimeOnly? ExtractFirstTimeFromEmploymentLine(string text, string label, int occurrence)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.Contains(label, StringComparison.OrdinalIgnoreCase));
        if (line is null) return null;

        var times = Regex.Matches(line, @"\d{1,2}[:.]\d{2}")
            .Select(x => TryParseTime(x.Value))
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
        return times.Count > occurrence ? times[occurrence] : null;
    }

    private static (int? Value, int Confidence) ExtractEmploymentTotalDuration(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => x.Contains("ZATRUDNIENIE KIEROWCY", StringComparison.OrdinalIgnoreCase));
        if (line is null) return (null, 0);

        var tokens = Regex.Matches(line, @"\d{1,2}:\d{2}\s*h?|\d{1,2}\s*h(?:\s*\d{1,2}\s*min)?|\d{1,4}\s*min")
            .Select(x => x.Value)
            .ToList();
        if (tokens.Count < 3) return (null, 0);

        var parsed = ParseMinutes(tokens[^1]);
        return (parsed, parsed.HasValue ? 100 : 0);
    }
    private static (int? Value, int Confidence) ExtractEmploymentDuration(string text, params string[] labels)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(x => labels.Any(label => x.Contains(label, StringComparison.OrdinalIgnoreCase)));
        if (line is null) return (null, 0);

        var tokens = Regex.Matches(line, @"\d{1,2}:\d{2}\s*h?|\d{1,2}\s*h(?:\s*\d{1,2}\s*min)?|\d{1,4}\s*min")
            .Select(x => x.Value)
            .ToList();
        if (tokens.Count == 0) return (null, 0);

        var parsed = ParseMinutes(tokens[^1]);
        return (parsed, parsed.HasValue ? 100 : 0);
    }
    private static string ExtractSection(string text, string heading)
    {
        var index = CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, heading, CompareOptions.IgnoreCase);
        return index < 0 ? string.Empty : text[index..];
    }

    private static DateOnly? ExtractValidFrom(string text)
    {
        var match = Regex.Match(text, @"(?i)(?:WAŻNA\s+OD(?:\s+DNIA)?|WAZNA\s+OD(?:\s+DNIA)?|ważna\s+od(?:\s+dnia)?|wazna\s+od(?:\s+dnia)?|\bod\b)\s+(?<date>\d{1,4}[.]\d{1,2}[.]\d{1,4})");
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["date"].Value;
        foreach (var format in new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy.MM.dd", "yyyy.M.d" })
        {
            if (DateOnly.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
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
        var explicitCodes = Regex.Matches(text, @"(?i)\bK\s*-?\s*(?<number>\d{1,4})(?<variant>bis)?\b")
            .Select(x => ("K-" + x.Groups["number"].Value + x.Groups["variant"].Value).ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labeledNumbers = Regex.Matches(text, @"(?i)\b(?:linia|line|l|trasa|kurs)\s*[:\-]?\s*(?<number>\d{1,4})(?<variant>bis)?\b")
            .Select(x => ("K-" + x.Groups["number"].Value + x.Groups["variant"].Value).ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var codes = explicitCodes.Count > 0 ? explicitCodes : labeledNumbers;

        return (codes.Select(code => new PlanningDutyLineDto
        {
            Id = Guid.NewGuid(),
            LineCode = code,
            Variant = code.EndsWith("bis", StringComparison.OrdinalIgnoreCase) ? "bis" : null
        }).ToList(), explicitCodes.Count > 0 ? 90 : codes.Count > 0 ? 70 : 0);
    }
    private static (List<PlanningDutyStopDto> Values, int Confidence) ExtractTransportStops(
        string text,
        IReadOnlyCollection<string> lineCodes)
    {
        var stops = new List<PlanningDutyStopDto>();
        PlanningDutyStopDto? lastStop = null;
        var knownHeadings = new[]
        {
            "SŁUŻBA", "SLUZBA", "WAŻNA", "WAZNA", "Autobus", "DZIENNY", "ZATRUDNIENIE", "KIEROWCY", "czas", "praca", "przerwa", "przer.śniad", "Przystanki"
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

            if (Regex.IsMatch(line, @"^\d{1,2}[:.]\d{2}$") && lastStop is not null)
            {
                lastStop.DepartureTime ??= TryParseTime(line);
                continue;
            }

            var match = Regex.Match(line, @"^(?<stop>[\p{L}0-9,. /\-]+?)\s+(?<km>\d+(?:[,.]\d+)?)\s+(?<times>(?:\d{1,2}[:.]\d{2}\s*)+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                match = Regex.Match(line, @"^(?<km>\d+(?:[,.]\d+)?)\s+(?<stop>[\p{L}0-9,. /\-]+?)\s+(?<times>(?:\d{1,2}[:.]\d{2}\s*)+)$", RegexOptions.IgnoreCase);
            }

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

            lastStop = new PlanningDutyStopDto
            {
                Id = Guid.NewGuid(),
                Sequence = stops.Count + 1,
                StopName = stopName,
                Km = km,
                ArrivalTime = times.Count > 1 ? times[0] : null,
                DepartureTime = times.Count > 0 ? times[^1] : null,
                LineCode = lineCodes.Count == 1 ? lineCodes.First() : null
            };
            stops.Add(lastStop);
        }

        return (stops, stops.Count > 0 ? 90 : 0);
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
            @"(?im)\b(?:(?:służba|sluzba)\s*(?:nr\s*)?|nr\s+służby\s*:\s*|nr\s+sluzby\s*:\s*|duty\s+|zadanie\s+|kurs\s+)[\s\-_:]*?(?<number>\d{1,5}[A-Za-z0-9\-/]*)")
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
        var hOnlyMatch = Regex.Match(value, @"^(?<h>\d{1,2})\s*h$");
        if (hOnlyMatch.Success) return int.Parse(hOnlyMatch.Groups["h"].Value, CultureInfo.InvariantCulture) * 60;

        var hMatch = Regex.Match(value, @"(?<h>\d{1,2})\s*h\s*(?<m>\d{1,2})(?:\s*min)?");
        if (hMatch.Success) return int.Parse(hMatch.Groups["h"].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(hMatch.Groups["m"].Value, CultureInfo.InvariantCulture);

        if (value.Contains(':'))
        {
            value = Regex.Replace(value, @"\s*h$", string.Empty).Trim();
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
            .Select(x => NormalizeLineCode(x.Groups["code"].Value.Trim()))
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var codes = labeled.Count > 0 ? labeled : Regex.Matches(text, @"(?i)\b(?<code>[A-Z]{1,3}-\d{1,4}(?:bis)?)\b")
            .Select(x => NormalizeLineCode(x.Groups["code"].Value.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (codes.Select(code => new PlanningDutyLineDto
        {
            Id = Guid.NewGuid(),
            LineCode = code,
            Variant = code.EndsWith("bis", StringComparison.OrdinalIgnoreCase) ? "bis" : null
        }).ToList(), labeled.Count > 0 ? 100 : codes.Count > 0 ? 50 : 0);
    }

    private static string NormalizeLineCode(string code)
    {
        var normalized = code.Trim();
        var match = Regex.Match(normalized, @"(?i)^K(?<number>\d{1,4})(?<variant>bis)?$");
        return match.Success
            ? "K-" + match.Groups["number"].Value + match.Groups["variant"].Value
            : normalized;
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




















