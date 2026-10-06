using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class CountryEntryCompletenessRule : ICountryEntryComplianceRule
{
    private static readonly TimeZoneInfo PolishTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Central European Standard Time" : "Europe/Warsaw");

    private const string MissingStartCountryCode = "MISSING_START_COUNTRY";
    private const string MissingEndCountryCode = "MISSING_END_COUNTRY";
    private const string InvalidCountryCode = "INVALID_COUNTRY_CODE";
    private const string IncompleteCountryDataCode = "INCOMPLETE_COUNTRY_DATA";
    private const string StartEntryType = "Start";
    private const string EndEntryType = "End";
    private const string UnknownEntryType = "Unknown";

    public string Code => IncompleteCountryDataCode;

    public string Name => "Niepełne dane kraju";

    private readonly ILogger<CountryEntryCompletenessRule> _logger;

    public CountryEntryCompletenessRule(ILogger<CountryEntryCompletenessRule> logger)
    {
        _logger = logger;
    }

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline,
        IReadOnlyList<ComplianceCountryEntry> countryEntries)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var activeDays = GetActiveDays(timeline);
        if (activeDays.Count == 0)
        {
            return result;
        }

        // Consecutive card downloads contain overlapping historical place records.
        // Treat the same physical card event as one entry even when it was imported
        // from several DDD files; otherwise Start, Start, End, End is incorrectly
        // reported as a missing end followed by a missing start.
        var uniqueCountryEntries = DeduplicateCountryEntries(countryEntries);

        var entriesByDay = uniqueCountryEntries
            .GroupBy(x => x.EntryTimeUtc.Date)
            .ToDictionary(x => x.Key, x => x.OrderBy(entry => entry.EntryTimeUtc).ToList());

        var activeDaySet = activeDays.ToHashSet();

        foreach (var day in activeDays)
        {
            entriesByDay.TryGetValue(day, out var dayEntries);
            dayEntries ??= [];

            var invalidEntries = dayEntries
                .Where(x => !IsValidCountryCode(x.CountryCode))
                .ToList();

            foreach (var invalidEntry in invalidEntries)
            {
                result.Violations.Add(CreateViolation(
                    code: InvalidCountryCode,
                    ruleName: "Nieprawidłowy kod kraju",
                    description: $"Kod kraju dla dnia {day:yyyy-MM-dd} jest pusty albo nierozpoznany.",
                    day: day,
                    entryTimeUtc: invalidEntry.EntryTimeUtc,
                    countryCode: invalidEntry.CountryCode,
                    entryType: NormalizeEntryType(invalidEntry.EntryType)));
            }
        }

        AddMissingStartOrEndViolations(result, uniqueCountryEntries, activeDaySet);

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: activeDays={ActiveDays}, countryEntries={CountryEntries}, warnings={WarningCount}.",
            Code,
            driverId,
            activeDays.Count,
            uniqueCountryEntries.Count,
            result.Violations.Count);

        return result;
    }

    private static IReadOnlyList<ComplianceCountryEntry> DeduplicateCountryEntries(
        IReadOnlyList<ComplianceCountryEntry> countryEntries)
    {
        return countryEntries
            .Where(x => x.EntryTimeUtc != default)
            .GroupBy(x => new
            {
                EntryTimeUtc = EnsureUtc(x.EntryTimeUtc),
                EntryType = NormalizeEntryType(x.EntryType)
            })
            // The same physical place record can be stored by several overlapping card
            // downloads. Older imports may contain an empty/unrecognized country while a
            // newer parse of that exact event contains the valid value. Treat timestamp and
            // entry type as the event identity and prefer the valid representation.
            .Select(x => x
                .OrderByDescending(entry => IsValidCountryCode(entry.CountryCode))
                .ThenBy(entry => NormalizeCountryCode(entry.CountryCode), StringComparer.Ordinal)
                .First())
            .OrderBy(x => x.EntryTimeUtc)
            .ToList();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string NormalizeCountryCode(string? countryCode) =>
        countryCode?.Trim().ToUpperInvariant() ?? string.Empty;

    private static void AddMissingStartOrEndViolations(
        ComplianceRuleResult result,
        IReadOnlyList<ComplianceCountryEntry> countryEntries,
        IReadOnlySet<DateTime> activeDays)
    {
        var typedEntries = countryEntries
            .Where(x => x.EntryTimeUtc != default)
            .Where(x => !IsUnknownEntry(x.EntryType))
            .OrderBy(x => x.EntryTimeUtc)
            .ToList();

        ComplianceCountryEntry? pendingStart = null;

        foreach (var entry in typedEntries)
        {
            if (IsStartEntry(entry.EntryType))
            {
                if (pendingStart is not null)
                {
                    AddMissingEndViolation(result, pendingStart, activeDays);
                }

                pendingStart = entry;
                continue;
            }

            if (pendingStart is not null && entry.EntryTimeUtc >= pendingStart.EntryTimeUtc)
            {
                // Place records describe the beginning and end of a daily work period, not
                // necessarily a single card-insertion session. A driver can leave the card in
                // the tachograph and the recorded interval may exceed 24 hours (including
                // manually entered activity). Pair by chronology; duration alone is not proof
                // that either country entry is missing.
                pendingStart = null;
                continue;
            }

            if (pendingStart is not null)
            {
                AddMissingEndViolation(result, pendingStart, activeDays);
                pendingStart = null;
            }

            AddMissingStartViolation(result, entry, activeDays);
        }

        if (pendingStart is not null)
        {
            AddMissingEndViolation(result, pendingStart, activeDays);
        }
    }

    private static void AddMissingStartViolation(
        ComplianceRuleResult result,
        ComplianceCountryEntry endEntry,
        IReadOnlySet<DateTime> activeDays)
    {
        if (!IsValidCountryCode(endEntry.CountryCode))
        {
            // The invalid-code warning already describes the reliable problem. Do not infer
            // an additional missing counterpart from an entry whose own data is incomplete.
            return;
        }

        var day = endEntry.EntryTimeUtc.Date;
        if (!activeDays.Contains(day))
        {
            return;
        }

        result.Violations.Add(CreateViolation(
            code: MissingStartCountryCode,
            ruleName: "Brak kraju rozpoczęcia",
            description: $"Dla okresu pracy zakończonego {FormatPolishLocalTime(endEntry.EntryTimeUtc)} brakuje wiarygodnego wpisu kraju rozpoczęcia.",
            day: day,
            entryTimeUtc: endEntry.EntryTimeUtc,
            countryCode: endEntry.CountryCode,
            entryType: EndEntryType));
    }

    private static void AddMissingEndViolation(
        ComplianceRuleResult result,
        ComplianceCountryEntry startEntry,
        IReadOnlySet<DateTime> activeDays)
    {
        if (!IsValidCountryCode(startEntry.CountryCode))
        {
            return;
        }

        var day = startEntry.EntryTimeUtc.Date;
        if (!activeDays.Contains(day))
        {
            return;
        }

        result.Violations.Add(CreateViolation(
            code: MissingEndCountryCode,
            ruleName: "Brak kraju zakończenia",
            description: $"Dla okresu pracy rozpoczętego {FormatPolishLocalTime(startEntry.EntryTimeUtc)} brakuje wiarygodnego wpisu kraju zakończenia.",
            day: day,
            entryTimeUtc: startEntry.EntryTimeUtc,
            countryCode: startEntry.CountryCode,
            entryType: StartEntryType));
    }

    private static string FormatPolishLocalTime(DateTime utcDateTime)
    {
        var normalizedUtc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, PolishTimeZone);

        return $"{localTime:yyyy-MM-dd HH:mm} czasu polskiego";
    }

    private static ComplianceViolationCandidate CreateViolation(
        string code,
        string ruleName,
        string description,
        DateTime day,
        DateTime? entryTimeUtc = null,
        string? countryCode = null,
        string? entryType = null)
    {
        var dayStartUtc = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        var periodStartUtc = entryTimeUtc ?? dayStartUtc;
        var periodEndUtc = dayStartUtc.AddDays(1);

        return new ComplianceViolationCandidate
        {
            Code = code,
            RuleName = ruleName,
            Severity = "Warning",
            Description = description,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            ActualMinutes = 0,
            LimitMinutes = 0,
            Metadata = new Dictionary<string, object>
            {
                ["countryEntryIssue"] = code,
                ["dayUtc"] = dayStartUtc.ToString("yyyy-MM-dd"),
                ["message"] = description,
                ["entryTimeUtc"] = entryTimeUtc?.ToString("O") ?? string.Empty,
                ["countryCode"] = countryCode ?? string.Empty,
                ["entryType"] = entryType ?? UnknownEntryType
            }
        };
    }

    private static IReadOnlyList<DateTime> GetActiveDays(IReadOnlyList<TimelineActivity> timeline)
    {
        var days = new SortedSet<DateTime>();

        foreach (var activity in timeline.Where(IsActivityRequiringCountryEntry))
        {
            var cursor = activity.StartUtc.Date;
            var lastDay = activity.EndUtc.AddTicks(-1).Date;

            while (cursor <= lastDay)
            {
                days.Add(DateTime.SpecifyKind(cursor, DateTimeKind.Utc));
                cursor = cursor.AddDays(1);
            }
        }

        return days.ToList();
    }

    private static bool IsActivityRequiringCountryEntry(TimelineActivity activity)
    {
        return activity.ActivityType.Equals(ActivityTypeNormalizer.Driving, StringComparison.OrdinalIgnoreCase) ||
            activity.ActivityType.Equals(ActivityTypeNormalizer.Work, StringComparison.OrdinalIgnoreCase) ||
            activity.ActivityType.Equals(ActivityTypeNormalizer.Availability, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStartEntry(string? entryType) =>
        NormalizeEntryType(entryType).Equals(StartEntryType, StringComparison.OrdinalIgnoreCase);

    private static bool IsEndEntry(string? entryType) =>
        NormalizeEntryType(entryType).Equals(EndEntryType, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnknownEntry(string? entryType) =>
        NormalizeEntryType(entryType).Equals(UnknownEntryType, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEntryType(string? entryType)
    {
        if (string.IsNullOrWhiteSpace(entryType))
        {
            return UnknownEntryType;
        }

        var normalized = entryType.Trim().ToUpperInvariant();

        if (normalized.Contains("START") ||
            normalized.Contains("BEGIN") ||
            normalized.Contains("INSERT") ||
            normalized.Contains("ROZPOCZ") ||
            normalized.Contains("WLOZ") ||
            normalized.Contains("WŁOŻ"))
        {
            return StartEntryType;
        }

        if (normalized.Contains("END") ||
            normalized.Contains("FINISH") ||
            normalized.Contains("WITHDRAW") ||
            normalized.Contains("REMOVE") ||
            normalized.Contains("ZAKON") ||
            normalized.Contains("ZAKOŃ".ToUpperInvariant()) ||
            normalized.Contains("WYJEC") ||
            normalized.Contains("WYJĘ".ToUpperInvariant()))
        {
            return EndEntryType;
        }

        return UnknownEntryType;
    }

    private static bool IsValidCountryCode(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        var normalized = countryCode.Trim();
        if (normalized is "?" or "??" or "---" ||
            normalized.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.Length is >= 1 and <= 3 &&
            normalized.All(char.IsLetterOrDigit);
    }
}
