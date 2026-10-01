using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class CountryEntryCompletenessRule : ICountryEntryComplianceRule
{
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

        var entriesByDay = countryEntries
            .Where(x => x.EntryTimeUtc != default)
            .GroupBy(x => x.EntryTimeUtc.Date)
            .ToDictionary(x => x.Key, x => x.OrderBy(entry => entry.EntryTimeUtc).ToList());

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

            var validEntries = dayEntries
                .Where(x => IsValidCountryCode(x.CountryCode))
                .ToList();

            if (validEntries.Count == 0)
            {
                continue;
            }

            // EntryType=Unknown is not evidence that the start or end country is missing.
            // Precise MISSING_START_COUNTRY/MISSING_END_COUNTRY warnings require a reliable opposite entry.
            var hasStartEntry = validEntries.Any(x => IsStartEntry(x.EntryType));
            var hasEndEntry = validEntries.Any(x => IsEndEntry(x.EntryType));

            if (!hasStartEntry && !hasEndEntry)
            {
                continue;
            }

            if (hasStartEntry && !hasEndEntry)
            {
                var startEntry = validEntries.First(x => IsStartEntry(x.EntryType));
                result.Violations.Add(CreateViolation(
                    code: MissingEndCountryCode,
                    ruleName: "Brak kraju zakończenia",
                    description: $"Dla dnia {day:yyyy-MM-dd} znaleziono wpis kraju rozpoczęcia, ale brakuje wiarygodnego wpisu zakończenia dnia pracy.",
                    day: day,
                    entryTimeUtc: startEntry.EntryTimeUtc,
                    countryCode: startEntry.CountryCode,
                    entryType: StartEntryType));
            }

            if (hasEndEntry && !hasStartEntry)
            {
                var endEntry = validEntries.First(x => IsEndEntry(x.EntryType));
                result.Violations.Add(CreateViolation(
                    code: MissingStartCountryCode,
                    ruleName: "Brak kraju rozpoczęcia",
                    description: $"Dla dnia {day:yyyy-MM-dd} znaleziono wpis kraju zakończenia, ale brakuje wiarygodnego wpisu rozpoczęcia dnia pracy.",
                    day: day,
                    entryTimeUtc: endEntry.EntryTimeUtc,
                    countryCode: endEntry.CountryCode,
                    entryType: EndEntryType));
            }
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: activeDays={ActiveDays}, countryEntries={CountryEntries}, warnings={WarningCount}.",
            Code,
            driverId,
            activeDays.Count,
            countryEntries.Count,
            result.Violations.Count);

        return result;
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
        var periodStartUtc = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        var periodEndUtc = periodStartUtc.AddDays(1);

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
                ["dayUtc"] = periodStartUtc.ToString("yyyy-MM-dd"),
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
