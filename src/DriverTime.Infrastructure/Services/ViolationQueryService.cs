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
        var businessDetails = BuildBusinessDetails(violation);
        var businessValues = BuildBusinessValues(violation, businessDetails);

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
            ActualValueMinutes = businessValues.ActualValueMinutes,
            RequiredValueMinutes = businessValues.RequiredValueMinutes,
            DifferenceMinutes = businessValues.DifferenceMinutes,
            MissingMinutes = businessValues.MissingMinutes,
            ExcessMinutes = businessValues.ExcessMinutes,
            CompensationMinutes = businessValues.CompensationMinutes,
            CompensationDeadlineUtc = businessValues.CompensationDeadlineUtc,
            BusinessSummary = businessValues.BusinessSummary,
            ScaleLabel = businessValues.ScaleLabel,
            DispatcherRecommendation = BuildDispatcherRecommendation(violation, businessValues),
            BusinessDetails = businessDetails
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

        if (code.Contains("CONTINUOUS_DRIVING", StringComparison.Ordinal))
        {
            return BuildContinuousDrivingBreakDetails(metadata);
        }

        if (code.Contains("DAILY_DRIVING", StringComparison.Ordinal) ||
            code.Contains("DAILY_DRIVING_LIMIT", StringComparison.Ordinal))
        {
            return BuildDailyDrivingDetails(metadata);
        }

        if (code.Contains("WEEKLY_REST_COMPENSATION", StringComparison.Ordinal))
        {
            return BuildWeeklyRestCompensationDetails(metadata);
        }

        if (code.Contains("WEEKLY_REST", StringComparison.Ordinal) ||
            code.Contains("REGULAR_WEEKLY_REST", StringComparison.Ordinal) ||
            code.Contains("REDUCED_WEEKLY_REST", StringComparison.Ordinal))
        {
            return BuildWeeklyRestDetails(metadata);
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

    private static ViolationBusinessDetailsDto? BuildContinuousDrivingBreakDetails(
        IReadOnlyDictionary<string, JsonElement> metadata)
    {
        var continuousDrivingMinutes = GetLong(metadata, "continuousDrivingMinutes");
        var requiredBreakMinutes = GetLong(metadata, "requiredBreakMinutes");
        var receivedBreakMinutes = GetLong(metadata, "receivedBreakMinutes");
        var exceededMinutes = GetLong(metadata, "exceededMinutes");
        var breakType = GetString(metadata, "breakType") ?? string.Empty;

        if (!continuousDrivingMinutes.HasValue)
        {
            return null;
        }

        var summary = $"Ciągła jazda wyniosła {FormatMinutes(continuousDrivingMinutes.Value)}.";
        if (requiredBreakMinutes.HasValue)
        {
            summary += $" Wymagana przerwa: {FormatMinutes(requiredBreakMinutes.Value)} albo 15 + 30 min.";
        }

        if (receivedBreakMinutes.HasValue && receivedBreakMinutes.Value > 0)
        {
            summary += $" Odebrana przerwa przed naruszeniem: {FormatMinutes(receivedBreakMinutes.Value)}.";
        }

        if (exceededMinutes.HasValue && exceededMinutes.Value > 0)
        {
            summary += $" Przekroczenie limitu: {FormatMinutes(exceededMinutes.Value)}.";
        }

        return new ViolationBusinessDetailsDto
        {
            ContinuousDrivingMinutes = continuousDrivingMinutes,
            RequiredBreakMinutes = requiredBreakMinutes,
            ReceivedBreakMinutes = receivedBreakMinutes,
            DrivingExceededMinutes = exceededMinutes,
            BreakType = breakType,
            Summary = summary
        };
    }

    private static ViolationBusinessDetailsDto? BuildDailyDrivingDetails(
        IReadOnlyDictionary<string, JsonElement> metadata)
    {
        var totalDrivingMinutes = GetLong(metadata, "totalDrivingMinutes");
        var limitMinutes = GetLong(metadata, "limitMinutes");
        var exceededMinutes = GetLong(metadata, "exceededMinutes");

        if (!totalDrivingMinutes.HasValue)
        {
            return null;
        }

        var summary = $"Dzienny czas jazdy wyniósł {FormatMinutes(totalDrivingMinutes.Value)}.";
        if (limitMinutes.HasValue)
        {
            summary += $" Limit dla tego naruszenia: {FormatMinutes(limitMinutes.Value)}.";
        }

        if (exceededMinutes.HasValue && exceededMinutes.Value > 0)
        {
            summary += $" Przekroczenie limitu: {FormatMinutes(exceededMinutes.Value)}.";
        }

        return new ViolationBusinessDetailsDto
        {
            ContinuousDrivingMinutes = totalDrivingMinutes,
            DrivingLimitMinutes = limitMinutes,
            DrivingExceededMinutes = exceededMinutes,
            Summary = summary
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

    private static ViolationBusinessDetailsDto? BuildWeeklyRestDetails(
        IReadOnlyDictionary<string, JsonElement> metadata)
    {
        var actualRestMinutes = GetLong(metadata, "actualRestMinutes")
            ?? GetLong(metadata, "longestRestMinutes")
            ?? GetLong(metadata, "reducedRestMinutes");
        var requiredRestMinutes = GetLong(metadata, "requiredRestMinutes")
            ?? GetLong(metadata, "requiredRegularWeeklyRestMinutes")
            ?? GetLong(metadata, "requiredReducedWeeklyRestMinutes");
        var missingRestMinutes = GetLong(metadata, "missingRestMinutes");

        if (!actualRestMinutes.HasValue && !requiredRestMinutes.HasValue && !missingRestMinutes.HasValue)
        {
            return null;
        }

        if (!missingRestMinutes.HasValue && actualRestMinutes.HasValue && requiredRestMinutes.HasValue)
        {
            missingRestMinutes = Math.Max(requiredRestMinutes.Value - actualRestMinutes.Value, 0);
        }

        var summaryParts = new List<string>();
        if (actualRestMinutes.HasValue)
        {
            summaryParts.Add($"Najdłuższy odpoczynek tygodniowy wyniósł {FormatMinutes(actualRestMinutes.Value)}.");
        }

        if (requiredRestMinutes.HasValue)
        {
            summaryParts.Add($"Wymagane minimum: {FormatMinutes(requiredRestMinutes.Value)}.");
        }

        if (missingRestMinutes.HasValue && missingRestMinutes.Value > 0)
        {
            summaryParts.Add($"Brakowało {FormatMinutes(missingRestMinutes.Value)}.");
        }

        return new ViolationBusinessDetailsDto
        {
            ActualRestMinutes = actualRestMinutes,
            RequiredRestMinutes = requiredRestMinutes,
            MissingRestMinutes = missingRestMinutes,
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

    private sealed record BusinessViolationValues(
        long? ActualValueMinutes,
        long? RequiredValueMinutes,
        long? DifferenceMinutes,
        long? MissingMinutes,
        long? ExcessMinutes,
        long? CompensationMinutes,
        DateTime? CompensationDeadlineUtc,
        string BusinessSummary,
        string ScaleLabel);

    private static BusinessViolationValues BuildBusinessValues(
        Violation violation,
        ViolationBusinessDetailsDto? details)
    {
        var metadata = ParseMetadata(violation.MetadataJson);
        var actualValueMinutes = details?.ActualRestMinutes
            ?? details?.ContinuousDrivingMinutes
            ?? GetLong(metadata, "actualRestMinutes")
            ?? GetLong(metadata, "longestRestMinutes")
            ?? GetLong(metadata, "restMinutes")
            ?? GetLong(metadata, "continuousDrivingMinutes")
            ?? GetLong(metadata, "totalDrivingMinutes");
        var requiredValueMinutes = details?.RequiredRestMinutes
            ?? details?.RequiredBreakMinutes
            ?? details?.DrivingLimitMinutes
            ?? GetLong(metadata, "requiredRestMinutes")
            ?? GetLong(metadata, "requiredRegularRestMinutes")
            ?? GetLong(metadata, "requiredReducedRestMinutes")
            ?? GetLong(metadata, "requiredRegularWeeklyRestMinutes")
            ?? GetLong(metadata, "requiredBreakMinutes")
            ?? GetLong(metadata, "limitMinutes");
        var missingMinutes = details?.MissingRestMinutes
            ?? GetLong(metadata, "missingRestMinutes")
            ?? GetLong(metadata, "missingCompensationMinutes");
        var excessMinutes = details?.DrivingExceededMinutes
            ?? GetLong(metadata, "exceededMinutes");
        var compensationMinutes = details?.CompensationDebtMinutes
            ?? GetLong(metadata, "compensationDebtMinutes")
            ?? GetLong(metadata, "missingCompensationMinutes");
        var compensationDeadlineUtc = details?.CompensationDeadlineUtc
            ?? GetDateTime(metadata, "compensationDeadlineUtc")
            ?? GetDateTime(metadata, "compensationDueDate");

        if (!missingMinutes.HasValue && actualValueMinutes.HasValue && requiredValueMinutes.HasValue)
        {
            missingMinutes = Math.Max(requiredValueMinutes.Value - actualValueMinutes.Value, 0);
        }

        if (!excessMinutes.HasValue && actualValueMinutes.HasValue && requiredValueMinutes.HasValue)
        {
            excessMinutes = Math.Max(actualValueMinutes.Value - requiredValueMinutes.Value, 0);
        }

        var differenceMinutes = actualValueMinutes.HasValue && requiredValueMinutes.HasValue
            ? actualValueMinutes.Value - requiredValueMinutes.Value
            : missingMinutes.HasValue
                ? -missingMinutes.Value
                : excessMinutes;
        var businessSummary = details?.Summary ?? string.Empty;
        var scaleLabel = BuildScaleLabel(missingMinutes, excessMinutes, compensationMinutes, compensationDeadlineUtc);

        return new BusinessViolationValues(
            actualValueMinutes,
            requiredValueMinutes,
            differenceMinutes,
            missingMinutes,
            excessMinutes,
            compensationMinutes,
            compensationDeadlineUtc,
            businessSummary,
            scaleLabel);
    }

    private static DispatcherRecommendationDto BuildDispatcherRecommendation(
        Violation violation,
        BusinessViolationValues values)
    {
        var code = violation.RegulationReference.ToUpperInvariant();
        var isBlocked = values.MissingMinutes.GetValueOrDefault() >= 180 ||
            values.ExcessMinutes.GetValueOrDefault() >= 60;
        var status = isBlocked ? "BLOCKED" : "HIGH_RISK";
        var canDrive = false;
        var canStartShift = false;
        var plannerAttentionRequired = true;
        DateTimeOffset? earliestNextDriveUtc = null;
        var actions = new List<string>();
        var summary = "Naruszenie wymaga weryfikacji dyspozytora przed dalszym planowaniem pracy.";

        if (code.Contains("DAILY_REST", StringComparison.Ordinal))
        {
            summary = "Kierowca nie odebrał wymaganego odpoczynku dziennego.";
            actions.Add("Zaplanuj pełny odpoczynek dzienny.");
            actions.Add("Nie planuj kolejnej zmiany przed odebraniem odpoczynku.");
            actions.Add("Zweryfikuj najwcześniejszy bezpieczny czas rozpoczęcia kolejnej jazdy.");
            earliestNextDriveUtc = values.MissingMinutes.HasValue && values.MissingMinutes.Value > 0
                ? new DateTimeOffset(violation.ViolationEnd, TimeSpan.Zero).AddMinutes(values.MissingMinutes.Value)
                : null;
        }
        else if (code.Contains("CONTINUOUS_DRIVING", StringComparison.Ordinal))
        {
            status = values.ExcessMinutes.GetValueOrDefault() > 0 ? "HIGH_RISK" : "WARNING";
            summary = "Kierowca przekroczył limit jazdy bez wymaganej przerwy.";
            actions.Add("Zaplanuj przerwę minimum 45 minut.");
            actions.Add("Możesz zastosować układ 15 + 30 minut, jeśli pierwsza część ma co najmniej 15 minut.");
            actions.Add("Nie rozpoczynaj kolejnego odcinka jazdy bez wymaganej przerwy.");
        }
        else if (code.Contains("DAILY_DRIVING", StringComparison.Ordinal))
        {
            summary = "Kierowca przekroczył dzienny limit jazdy.";
            actions.Add("Zakończ jazdę i zaplanuj odpoczynek dzienny.");
            actions.Add("Rozważ przekazanie kolejnego zlecenia innemu kierowcy.");
            actions.Add("Sprawdź, czy wydłużenie dnia jazdy było dopuszczalne w tym tygodniu.");
        }
        else if (code.Contains("WEEKLY_DRIVING", StringComparison.Ordinal))
        {
            summary = "Kierowca przekroczył tygodniowy limit jazdy.";
            actions.Add("Nie planuj kolejnych tras w bieżącym tygodniu.");
            actions.Add("Zaplanuj odpoczynek i sprawdź limit dwutygodniowy.");
        }
        else if (code.Contains("WEEKLY_REST_COMPENSATION", StringComparison.Ordinal) ||
            code.Contains("REDUCED_WEEKLY_REST_COMPENSATION", StringComparison.Ordinal))
        {
            status = "HIGH_RISK";
            summary = values.CompensationDeadlineUtc.HasValue
                ? $"Kierowca ma brakującą rekompensatę odpoczynku tygodniowego do {values.CompensationDeadlineUtc.Value:dd.MM.yyyy}."
                : "Kierowca ma brakującą rekompensatę odpoczynku tygodniowego.";
            actions.Add("Zaplanuj rekompensatę brakującego odpoczynku.");
            actions.Add("Dołącz rekompensatę do odpoczynku trwającego minimum 9 godzin.");
            if (values.CompensationDeadlineUtc.HasValue)
            {
                actions.Add($"Dopilnuj terminu rekompensaty do {values.CompensationDeadlineUtc.Value:dd.MM.yyyy}.");
            }
        }
        else if (code.Contains("REGULAR_WEEKLY_REST", StringComparison.Ordinal) ||
            code.Contains("REDUCED_WEEKLY_REST", StringComparison.Ordinal) ||
            code.Contains("WEEKLY_REST", StringComparison.Ordinal))
        {
            summary = "Kierowca nie odebrał wymaganego odpoczynku tygodniowego.";
            actions.Add("Zaplanuj odpoczynek tygodniowy przed kolejną trasą.");
            actions.Add("Sprawdź, czy odpoczynek może być skrócony i czy wymaga późniejszej rekompensaty.");
        }
        else if (code.Contains("SIX_24H_PERIODS", StringComparison.Ordinal) ||
            code.Contains("SIX_24", StringComparison.Ordinal))
        {
            summary = "Kierowca przekroczył dopuszczalny czas do rozpoczęcia odpoczynku tygodniowego.";
            actions.Add("Zaplanuj odpoczynek tygodniowy bez dalszego opóźniania.");
            actions.Add("Nie dokładaj kolejnych zleceń przed weryfikacją planu tygodnia.");
        }
        else if (IsCountryDataViolation(violation))
        {
            status = "WARNING";
            canDrive = true;
            canStartShift = true;
            plannerAttentionRequired = true;

            if (IsMissingStartCountryViolation(violation))
            {
                summary = "Brakuje wpisu kraju rozpoczęcia dnia pracy.";
                actions.Add("Zweryfikuj z kierowcą kraj rozpoczęcia pracy.");
                actions.Add("Uzupełnij lub odnotuj brakujący wpis kraju w dokumentacji.");
                actions.Add("Przypomnij kierowcy o obowiązku wyboru kraju rozpoczęcia w tachografie.");
                actions.Add("Sprawdź, czy problem nie powtarza się w kolejnych dniach.");
            }
            else if (IsMissingEndCountryViolation(violation))
            {
                summary = "Brakuje wpisu kraju zakończenia dnia pracy.";
                actions.Add("Zweryfikuj z kierowcą kraj zakończenia pracy.");
                actions.Add("Uzupełnij lub odnotuj brakujący wpis kraju w dokumentacji.");
                actions.Add("Przypomnij kierowcy o obowiązku wyboru kraju zakończenia w tachografie.");
                actions.Add("Sprawdź, czy problem nie powtarza się w kolejnych dniach.");
            }
            else
            {
                summary = "Wykryto problem z kompletnością danych tachografu.";
                actions.Add("Zweryfikuj dane źródłowe z odczytu karty lub tachografu.");
                actions.Add("Sprawdź, czy import DDD zakończył się poprawnie.");
                actions.Add("W razie potrzeby wykonaj ponowny odczyt danych.");
            }
        }
        else
        {
            status = "WARNING";
            canDrive = true;
            canStartShift = true;
            actions.Add("Zweryfikuj naruszenie z timeline aktywności i danymi DDD.");
            actions.Add("Dodaj notatkę operacyjną, jeśli naruszenie ma uzasadnienie.");
        }

        if (actions.Count == 0)
        {
            actions.Add("Zweryfikuj timeline aktywności kierowcy przed dalszym planowaniem.");
        }

        return new DispatcherRecommendationDto
        {
            Status = status,
            Summary = summary,
            RecommendedActions = actions,
            CanDrive = canDrive,
            CanStartShift = canStartShift,
            PlannerAttentionRequired = plannerAttentionRequired,
            EarliestNextDriveUtc = earliestNextDriveUtc
        };
    }

    private static bool IsCountryDataViolation(Violation violation)
    {
        var code = violation.RegulationReference.ToUpperInvariant();
        var type = violation.ViolationType.ToUpperInvariant();
        var description = BuildDescription(violation).ToUpperInvariant();

        return code.Contains("COUNTRY", StringComparison.Ordinal) ||
            type.Contains("COUNTRY", StringComparison.Ordinal) ||
            description.Contains("KRAJU", StringComparison.Ordinal) ||
            description.Contains("KOD KRAJU", StringComparison.Ordinal) ||
            description.Contains("DANE KRAJU", StringComparison.Ordinal);
    }

    private static bool IsMissingStartCountryViolation(Violation violation)
    {
        var code = violation.RegulationReference.ToUpperInvariant();
        var type = violation.ViolationType.ToUpperInvariant();
        var description = BuildDescription(violation).ToUpperInvariant();

        return code.Contains("MISSING_START_COUNTRY", StringComparison.Ordinal) ||
            type.Contains("BRAK KRAJU ROZPOCZ", StringComparison.Ordinal) ||
            description.Contains("BRAKUJE WPISU KRAJU ROZPOCZ", StringComparison.Ordinal) ||
            description.Contains("BRAK KRAJU ROZPOCZ", StringComparison.Ordinal);
    }

    private static bool IsMissingEndCountryViolation(Violation violation)
    {
        var code = violation.RegulationReference.ToUpperInvariant();
        var type = violation.ViolationType.ToUpperInvariant();
        var description = BuildDescription(violation).ToUpperInvariant();

        return code.Contains("MISSING_END_COUNTRY", StringComparison.Ordinal) ||
            type.Contains("BRAK KRAJU ZAKO", StringComparison.Ordinal) ||
            description.Contains("BRAKUJE WPISU KRAJU ZAKO", StringComparison.Ordinal) ||
            description.Contains("BRAK KRAJU ZAKO", StringComparison.Ordinal);
    }

    private static string BuildScaleLabel(
        long? missingMinutes,
        long? excessMinutes,
        long? compensationMinutes,
        DateTime? compensationDeadlineUtc)
    {
        if (excessMinutes.HasValue && excessMinutes.Value > 0)
        {
            return $"+{FormatMinutes(excessMinutes.Value)}";
        }

        if (missingMinutes.HasValue && missingMinutes.Value > 0)
        {
            return $"brakuje {FormatMinutes(missingMinutes.Value)}";
        }

        if (compensationMinutes.HasValue && compensationMinutes.Value > 0)
        {
            return $"rekompensata {FormatMinutes(compensationMinutes.Value)}";
        }

        if (compensationDeadlineUtc.HasValue)
        {
            return $"do {compensationDeadlineUtc.Value:dd.MM.yyyy}";
        }

        return string.Empty;
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
            DateTimeOffset.TryParse(value.GetString(), out var parsed))
        {
            return parsed.UtcDateTime;
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
