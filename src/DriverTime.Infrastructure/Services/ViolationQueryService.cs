using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class ViolationQueryService : IViolationQueryService
{
    private readonly DriverTimeDbContext _dbContext;

    public ViolationQueryService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
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
            MetadataJson = violation.MetadataJson
        };
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
