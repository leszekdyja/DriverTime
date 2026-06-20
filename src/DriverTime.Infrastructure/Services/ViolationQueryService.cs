using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DriverTime.Infrastructure.Services;

public class ViolationQueryService : IViolationQueryService
{
    private const int DefaultQueryRangeDays = 60;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ILogger<ViolationQueryService> _logger;

    public ViolationQueryService(
        DriverTimeDbContext dbContext,
        ILogger<ViolationQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ViolationDto>> GetAsync(
        Guid companyId,
        Guid? driverId,
        DateTime? fromDate,
        DateTime? toDate,
        string? severity,
        string? type,
        CancellationToken cancellationToken = default)
    {
        var query = BuildCompanyQuery(companyId);
        var hasCustomRange = fromDate.HasValue || toDate.HasValue;

        if (!hasCustomRange)
        {
            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddDays(-DefaultQueryRangeDays);
            query = query.Where(x =>
                x.ViolationEnd >= fromUtc &&
                x.ViolationStart <= toUtc);
        }

        if (driverId.HasValue)
        {
            query = query.Where(x => x.DriverId == driverId.Value);
        }

        if (fromDate.HasValue)
        {
            var fromUtc = EnsureUtc(fromDate.Value);
            query = query.Where(x => x.ViolationEnd >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = EnsureUtc(toDate.Value).Date.AddDays(1);
            query = query.Where(x => x.ViolationStart < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var normalizedSeverity = NormalizeSeverity(severity);
            query = query.Where(x => x.Severity == normalizedSeverity);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim().ToLower();
            query = query.Where(x =>
                x.ViolationType.ToLower().Contains(normalizedType) ||
                x.RegulationReference.ToLower().Contains(normalizedType));
        }

        var violations = await query
            .OrderByDescending(x => x.ViolationStart)
            .Take(500)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Violation query returned {Count} rows for company {CompanyId}. DriverId={DriverId}, FromDate={FromDate:o}, ToDate={ToDate:o}, Severity={Severity}, Type={Type}.",
            violations.Count,
            companyId,
            driverId,
            fromDate,
            toDate,
            severity,
            type);

        return violations.Select(Map).ToList();
    }

    public async Task<ViolationDto?> GetByIdAsync(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var violation = await BuildCompanyQuery(companyId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return violation is null ? null : Map(violation);
    }

    private IQueryable<Violation> BuildCompanyQuery(Guid companyId)
    {
        return _dbContext.Violations
            .AsNoTracking()
            .Include(x => x.Driver)
            .Where(x => x.Driver != null && x.Driver.CompanyId == companyId);
    }

    private static ViolationDto Map(Violation violation)
    {
        return new ViolationDto
        {
            Id = violation.Id,
            DriverId = violation.DriverId,
            Code = violation.RegulationReference,
            DriverFirstName = violation.Driver?.FirstName ?? string.Empty,
            DriverLastName = violation.Driver?.LastName ?? string.Empty,
            DriverCardNumber = violation.Driver?.CardNumber ?? string.Empty,
            ViolationType = violation.ViolationType,
            OccurredAtUtc = violation.ViolationStart,
            PeriodEndUtc = violation.ViolationEnd,
            Description = BuildDescription(violation),
            Severity = NormalizeSeverity(violation.Severity),
            Recommendation = BuildRecommendation(violation.RegulationReference),
            DetectedAtUtc = violation.CalculatedAt,
            ActualDurationMinutes = violation.DurationMinutes,
            LimitDurationMinutes = GetLimitMinutes(violation.RegulationReference),
            MetadataJson = violation.MetadataJson,
            BusinessDetails = BuildBusinessDetails(violation)
        };
    }

    private static ViolationBusinessDetailsDto? BuildBusinessDetails(Violation violation)
    {
        var metadata = ParseMetadata(violation.MetadataJson);
        if (metadata.Count == 0)
        {
            return null;
        }

        var code = violation.RegulationReference.ToUpperInvariant();
        if (code.Contains("DAILY_REST", StringComparison.Ordinal))
        {
            return BuildDailyRestDetails(metadata);
        }

        if (code.Contains("WEEKLY_REST_COMPENSATION", StringComparison.Ordinal))
        {
            return BuildWeeklyRestCompensationDetails(metadata);
        }

        if (code.Contains("COUNTRY", StringComparison.Ordinal))
        {
            return BuildCountryEntryDetails(metadata, BuildDescription(violation));
        }

        return null;
    }

    private static ViolationBusinessDetailsDto? BuildDailyRestDetails(
        IReadOnlyDictionary<string, JsonElement> metadata)
    {
        var actualRestMinutes = GetLong(metadata, "longestRestMinutes")
            ?? GetLong(metadata, "restMinutes");
        var reason = GetString(metadata, "reason");
        var requiredRestMinutes = string.Equals(
                reason,
                "MissingContinuousReducedDailyRest",
                StringComparison.OrdinalIgnoreCase)
            ? GetLong(metadata, "requiredReducedRestMinutes")
            : GetLong(metadata, "requiredRegularRestMinutes");

        requiredRestMinutes ??= GetLong(metadata, "requiredRegularRestMinutes")
            ?? GetLong(metadata, "requiredReducedRestMinutes");

        if (!actualRestMinutes.HasValue || !requiredRestMinutes.HasValue)
        {
            return null;
        }

        var missingRestMinutes = Math.Max(requiredRestMinutes.Value - actualRestMinutes.Value, 0);

        return new ViolationBusinessDetailsDto
        {
            ActualRestMinutes = actualRestMinutes,
            RequiredRestMinutes = requiredRestMinutes,
            MissingRestMinutes = missingRestMinutes,
            Summary = missingRestMinutes > 0
                ? $"Odpoczynek wyniósł {FormatMinutes(actualRestMinutes.Value)}, wymagane minimum {FormatMinutes(requiredRestMinutes.Value)}. Brakowało {FormatMinutes(missingRestMinutes)}."
                : $"Odpoczynek wyniósł {FormatMinutes(actualRestMinutes.Value)}, wymagane minimum {FormatMinutes(requiredRestMinutes.Value)}."
        };
    }

    private static ViolationBusinessDetailsDto? BuildWeeklyRestCompensationDetails(
        IReadOnlyDictionary<string, JsonElement> metadata)
    {
        var reducedRestMinutes = GetLong(metadata, "reducedRestMinutes");
        var compensationDebtMinutes = GetLong(metadata, "compensationDebtMinutes")
            ?? GetLong(metadata, "missingCompensationMinutes");
        var compensationDeadlineUtc = GetDateTime(metadata, "compensationDeadlineUtc");

        if (!reducedRestMinutes.HasValue &&
            !compensationDebtMinutes.HasValue &&
            !compensationDeadlineUtc.HasValue)
        {
            return null;
        }

        var summaryParts = new List<string>();
        if (reducedRestMinutes.HasValue)
        {
            summaryParts.Add($"Skrócony odpoczynek tygodniowy wyniósł {FormatMinutes(reducedRestMinutes.Value)}.");
        }

        if (compensationDebtMinutes.HasValue)
        {
            summaryParts.Add($"Rekompensata do odebrania: {FormatMinutes(compensationDebtMinutes.Value)}.");
        }

        if (compensationDeadlineUtc.HasValue)
        {
            summaryParts.Add($"Rekompensatę należy odebrać do {compensationDeadlineUtc.Value:yyyy-MM-dd}.");
        }

        return new ViolationBusinessDetailsDto
        {
            ReducedWeeklyRestMinutes = reducedRestMinutes,
            CompensationDebtMinutes = compensationDebtMinutes,
            CompensationDeadlineUtc = compensationDeadlineUtc,
            Summary = string.Join(" ", summaryParts)
        };
    }

    private static ViolationBusinessDetailsDto? BuildCountryEntryDetails(
        IReadOnlyDictionary<string, JsonElement> metadata,
        string description)
    {
        var message = GetString(metadata, "message");
        if (string.IsNullOrWhiteSpace(message))
        {
            message = description;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return new ViolationBusinessDetailsDto
        {
            CountryIssueMessage = message,
            Summary = message
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseMetadata(string metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new Dictionary<string, JsonElement>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson)
                ?? new Dictionary<string, JsonElement>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, JsonElement>();
        }
    }

    private static long? GetLong(IReadOnlyDictionary<string, JsonElement> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateTime? GetDateTime(IReadOnlyDictionary<string, JsonElement> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(value.GetString(), out var parsed))
        {
            return parsed.Kind == DateTimeKind.Utc
                ? parsed
                : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var ticks) &&
            ticks > 0)
        {
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        return null;
    }

    private static string FormatMinutes(long minutes)
    {
        var safeMinutes = Math.Max(minutes, 0);
        var hours = safeMinutes / 60;
        var remainingMinutes = safeMinutes % 60;

        if (hours == 0)
        {
            return $"{remainingMinutes} min";
        }

        if (remainingMinutes == 0)
        {
            return $"{hours} godz.";
        }

        return $"{hours} godz. {remainingMinutes} min";
    }

    private static string BuildDescription(Violation violation)
    {
        return violation.RegulationReference switch
        {
            "EU561:DAILY_DRIVING_OVER_9H" =>
                "Dzienny czas jazdy przekroczył standardowy limit 9 godzin.",
            "EU561:DAILY_DRIVING_OVER_10H" =>
                "Dzienny czas jazdy przekroczył bezwzględny limit 10 godzin.",
            "EU561:CONTINUOUS_DRIVING_WITHOUT_45M_BREAK" =>
                "Kierowca nie odebrał wymaganej przerwy 45 minut po 4h30 jazdy.",
            "EU561:DAILY_REST_BELOW_11H" =>
                "Najdłuższy odpoczynek dzienny był krótszy niż 11 godzin.",
            "DAILY_DRIVING_LIMIT" =>
                "Dzienny czas jazdy przekroczył limit zgodności EU561/AETR.",
            "CONTINUOUS_DRIVING_BREAK" =>
                "Kierowca przekroczył limit ciągłej jazdy bez wymaganej przerwy.",
            "DAILY_REST" =>
                "Odpoczynek dzienny nie spełnia wymaganego limitu.",
            "EU561_WEEKLY_DRIVING_56H" or "WEEKLY_DRIVING_LIMIT" =>
                "Tygodniowy czas jazdy przekroczył limit 56 godzin.",
            "EU561_BIWEEKLY_DRIVING_90H" or "BI_WEEKLY_DRIVING_LIMIT" =>
                "Czas jazdy w dwóch kolejnych tygodniach przekroczył limit 90 godzin.",
            "MISSING_START_COUNTRY" =>
                "Brakuje wpisu kraju rozpoczęcia dnia pracy.",
            "MISSING_END_COUNTRY" =>
                "Brakuje wpisu kraju zakończenia dnia pracy.",
            "INVALID_COUNTRY_CODE" =>
                "Kod kraju jest pusty albo nierozpoznany.",
            "INCOMPLETE_COUNTRY_DATA" =>
                "Dane kraju rozpoczęcia lub zakończenia są niepełne.",
            _ => violation.ViolationType
        };
    }

    private static string BuildRecommendation(string code)
    {
        return code switch
        {
            "EU561:DAILY_DRIVING_OVER_10H" =>
                "Pilnie zweryfikuj plan pracy kierowcy i dokumentację z dnia naruszenia.",
            "EU561:CONTINUOUS_DRIVING_WITHOUT_45M_BREAK" =>
                "Sprawdź przebieg przerw i zaplanuj korektę harmonogramu kolejnych tras.",
            "EU561:DAILY_DRIVING_OVER_9H" =>
                "Sprawdź, czy wydłużenie jazdy było dopuszczalne i nie przekracza limitów tygodniowych.",
            "EU561:DAILY_REST_BELOW_11H" =>
                "Zweryfikuj odpoczynek dzienny i zapewnij odpowiednią przerwę przed kolejną trasą.",
            "DAILY_DRIVING_LIMIT" or
            "CONTINUOUS_DRIVING_BREAK" or
            "DAILY_REST" or
            "MISSING_START_COUNTRY" or
            "MISSING_END_COUNTRY" or
            "INVALID_COUNTRY_CODE" or
            "INCOMPLETE_COUNTRY_DATA" or
            "EU561_WEEKLY_DRIVING_56H" or
            "WEEKLY_DRIVING_LIMIT" or
            "EU561_BIWEEKLY_DRIVING_90H" or
            "BI_WEEKLY_DRIVING_LIMIT" =>
                "Zweryfikuj timeline aktywności kierowcy i zaplanuj działania korygujące.",
            _ => "Zweryfikuj naruszenie w kontekście aktywności kierowcy i danych DDD."
        };
    }

    private static long GetLimitMinutes(string code)
    {
        return code switch
        {
            "EU561:DAILY_DRIVING_OVER_9H" => 9 * 60,
            "EU561:DAILY_DRIVING_OVER_10H" => 10 * 60,
            "EU561:CONTINUOUS_DRIVING_WITHOUT_45M_BREAK" => 270,
            "EU561:DAILY_REST_BELOW_11H" => 11 * 60,
            "DAILY_DRIVING_LIMIT" => 9 * 60,
            "CONTINUOUS_DRIVING_BREAK" => 270,
            "DAILY_REST" => 11 * 60,
            "EU561_WEEKLY_DRIVING_56H" or "WEEKLY_DRIVING_LIMIT" => 56 * 60,
            "EU561_BIWEEKLY_DRIVING_90H" or "BI_WEEKLY_DRIVING_LIMIT" => 90 * 60,
            _ => 0
        };
    }

    private static string NormalizeSeverity(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "critical" or "very serious" or "very-serious" or "high" or "severe" => "Critical",
            "warning" or "serious" or "medium" => "Warning",
            _ => "Info"
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
