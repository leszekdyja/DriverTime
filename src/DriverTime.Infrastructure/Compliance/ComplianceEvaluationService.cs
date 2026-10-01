using System.Text.Json;
using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance;

public class ComplianceEvaluationService : IComplianceEvaluationService
{
    private static readonly string[] ReplaceableComplianceCodes =
    [
        "DAILY_DRIVING_LIMIT",
        "CONTINUOUS_DRIVING_BREAK",
        "DAILY_REST",
        "WEEKLY_DRIVING_LIMIT",
        "BI_WEEKLY_DRIVING_LIMIT",
        "EU561_WEEKLY_DRIVING_56H",
        "EU561_BIWEEKLY_DRIVING_90H",
        "MISSING_START_COUNTRY",
        "MISSING_END_COUNTRY",
        "INVALID_COUNTRY_CODE",
        "INCOMPLETE_COUNTRY_DATA"
    ];

    private const string ManualRecalculationTrigger = "manual-recalculate";

    private readonly DriverTimeDbContext _dbContext;
    private readonly IComplianceEngineService _complianceEngineService;
    private readonly IComplianceRunHistoryService _complianceRunHistoryService;
    private readonly ILogger<ComplianceEvaluationService> _logger;

    public ComplianceEvaluationService(
        DriverTimeDbContext dbContext,
        IComplianceEngineService complianceEngineService,
        IComplianceRunHistoryService complianceRunHistoryService,
        ILogger<ComplianceEvaluationService> logger)
    {
        _dbContext = dbContext;
        _complianceEngineService = complianceEngineService;
        _complianceRunHistoryService = complianceRunHistoryService;
        _logger = logger;
    }

    public async Task<int> EvaluateForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EVALUATE START driver={DriverId} company={CompanyId}",
            driverId,
            companyId);

        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == driverId && x.CompanyId == companyId,
                cancellationToken);

        if (!driverExists)
        {
            _logger.LogInformation(
                "Compliance evaluation skipped because driver {DriverId} was not found in company {CompanyId}.",
                driverId,
                companyId);

            return 0;
        }

        var preview = await _complianceEngineService.PreviewForDriverAsync(
            companyId,
            driverId,
            includeTimeline: true,
            cancellationToken: cancellationToken);

        if (preview is null)
        {
            return 0;
        }

        _logger.LogInformation(
            "Activities count={Count}",
            preview.Timeline.Count);

        _logger.LogInformation(
            "Compliance preview returned {Count} violations for driver {DriverId}.",
            preview.Violations.Count,
            driverId);

        var replaceableCodes = preview.DebugSummary.RegisteredRuleCodes
            .Concat(preview.Violations.Select(x => x.Code))
            .Concat(ReplaceableComplianceCodes)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
        var replacementRange = ResolveReplacementRange(preview);

        if (replacementRange is null)
        {
            _logger.LogInformation(
                "Compliance evaluation completed for driver {DriverId}. No replacement range was available. CompanyId={CompanyId}, Timeline={TimelineCount}, Violations={ViolationCount}.",
                driverId,
                companyId,
                preview.Timeline.Count,
                preview.Violations.Count);

            return 0;
        }

        var deletedCount = await _dbContext.Violations
            .Where(x =>
                x.DriverId == driverId &&
                x.Driver != null &&
                x.Driver.CompanyId == companyId &&
                replaceableCodes.Contains(x.RegulationReference) &&
                x.ViolationStart < replacementRange.EndUtc &&
                x.ViolationEnd > replacementRange.StartUtc)
            .ExecuteDeleteAsync(cancellationToken);

        if (preview.Violations.Count == 0)
        {
            _logger.LogInformation(
                "Compliance evaluation completed for driver {DriverId}. No violations detected. CompanyId={CompanyId}, Timeline={TimelineCount}, DeletedExisting={DeletedCount}, ReplaceableCodes={ReplaceableCodes}, ReplacementRange={ReplacementStartUtc:o}..{ReplacementEndUtc:o}.",
                driverId,
                companyId,
                preview.Timeline.Count,
                deletedCount,
                string.Join(", ", replaceableCodes),
                replacementRange.StartUtc,
                replacementRange.EndUtc);

            return 0;
        }

        var calculatedAt = DateTime.UtcNow;
        var violations = preview.Violations
            .Select(x => MapViolation(driverId, x, calculatedAt))
            .ToList();

        _dbContext.Violations.AddRange(violations);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Compliance evaluation completed for driver {DriverId}. CompanyId={CompanyId}, Timeline={TimelineCount}, DeletedExisting={DeletedCount}, Saved violations: {Count}, ReplacementRange={ReplacementStartUtc:o}..{ReplacementEndUtc:o}.",
            driverId,
            companyId,
            preview.Timeline.Count,
            deletedCount,
            violations.Count,
            replacementRange.StartUtc,
            replacementRange.EndUtc);

        return violations.Count;
    }

    public async Task<ComplianceDriverRecalculationResultDto?> RecalculateForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var driverExists = await _dbContext.Drivers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == driverId && x.CompanyId == companyId,
                cancellationToken);

        if (!driverExists)
        {
            _logger.LogInformation(
                "Manual compliance recalculation skipped because driver {DriverId} was not found in company {CompanyId}.",
                driverId,
                companyId);

            return null;
        }

        var preview = await _complianceEngineService.PreviewForDriverAsync(
            companyId,
            driverId,
            includeTimeline: true,
            cancellationToken: cancellationToken);

        if (preview is null)
        {
            return null;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var deletedCount = await DeletePersistedViolationsForDriverAsync(
            companyId,
            driverId,
            cancellationToken);

        var calculatedAt = DateTime.UtcNow;
        var violations = preview.Violations
            .Select(x => MapViolation(driverId, x, calculatedAt))
            .ToList();

        if (violations.Count > 0)
        {
            _dbContext.Violations.AddRange(violations);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _complianceRunHistoryService.SaveRunAsync(
            companyId,
            driverId,
            preview,
            ManualRecalculationTrigger,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Manual compliance recalculation completed for driver {DriverId}. CompanyId={CompanyId}, DeletedExisting={DeletedCount}, SavedViolations={SavedCount}.",
            driverId,
            companyId,
            deletedCount,
            violations.Count);

        return new ComplianceDriverRecalculationResultDto
        {
            DriverId = driverId,
            DeletedViolationsCount = deletedCount,
            SavedViolationsCount = violations.Count,
            Success = true,
            Message = violations.Count == 0
                ? "Przeliczono zgodność. Nie wykryto aktualnych naruszeń."
                : "Przeliczono zgodność i zapisano aktualne naruszenia."
        };
    }

    public async Task<ComplianceRecalculationResponseDto> RecalculateForCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var driverIds = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var response = new ComplianceRecalculationResponseDto
        {
            DriversCount = driverIds.Count
        };

        foreach (var driverId in driverIds)
        {
            var result = await RecalculateForDriverAsync(
                companyId,
                driverId,
                cancellationToken);

            if (result is null)
            {
                continue;
            }

            response.Drivers.Add(result);
            response.RecalculatedDriversCount++;
            response.DeletedViolationsCount += result.DeletedViolationsCount;
            response.SavedViolationsCount += result.SavedViolationsCount;
        }

        return response;
    }

    private Task<int> DeletePersistedViolationsForDriverAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Violations
            .Where(x =>
                x.DriverId == driverId &&
                x.Driver != null &&
                x.Driver.CompanyId == companyId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    internal static ReplacementRange? ResolveReplacementRange(
        CompliancePreviewResponseDto preview)
    {
        var starts = preview.Timeline
            .Select(x => x.StartUtc)
            .Concat(preview.Violations.Select(x => x.PeriodStartUtc))
            .ToList();
        var ends = preview.Timeline
            .Select(x => x.EndUtc)
            .Concat(preview.Violations.Select(x => x.PeriodEndUtc))
            .ToList();

        if (starts.Count == 0 || ends.Count == 0)
        {
            return null;
        }

        var startUtc = EnsureUtc(starts.Min());
        var endUtc = EnsureUtc(ends.Max());

        return endUtc > startUtc
            ? new ReplacementRange(startUtc, endUtc)
            : null;
    }

    internal static bool ShouldDeleteExistingViolation(
        Violation violation,
        Guid companyId,
        Guid driverId,
        ReplacementRange replacementRange,
        IReadOnlyCollection<string> replaceableCodes)
    {
        return violation.DriverId == driverId
            && violation.Driver?.CompanyId == companyId
            && replaceableCodes.Contains(violation.RegulationReference)
            && violation.ViolationStart < replacementRange.EndUtc
            && violation.ViolationEnd > replacementRange.StartUtc;
    }

    internal static bool ShouldDeleteExistingViolationForManualRecalculation(
        Violation violation,
        Guid companyId,
        Guid driverId)
    {
        return violation.DriverId == driverId
            && violation.Driver?.CompanyId == companyId;
    }

    internal static IReadOnlyList<Violation> BuildManualRecalculationSnapshot(
        IEnumerable<Violation> existingViolations,
        Guid companyId,
        Guid driverId,
        IEnumerable<Violation> currentViolations)
    {
        return existingViolations
            .Where(x => !ShouldDeleteExistingViolationForManualRecalculation(x, companyId, driverId))
            .Concat(currentViolations)
            .ToList();
    }

    internal static Violation MapViolation(
        Guid driverId,
        ComplianceViolationPreviewDto violation,
        DateTime calculatedAt)
    {
        return new Violation
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            ViolationType = string.IsNullOrWhiteSpace(violation.RuleName)
                ? violation.Code
                : violation.RuleName,
            RegulationReference = violation.Code,
            Severity = NormalizeSeverity(violation.Severity),
            DurationMinutes = ToInt32Minutes(violation.ActualMinutes),
            MetadataJson = JsonSerializer.Serialize(violation.Metadata),
            ViolationStart = EnsureUtc(violation.PeriodStartUtc),
            ViolationEnd = EnsureUtc(violation.PeriodEndUtc),
            CalculatedAt = calculatedAt
        };
    }

    private static string NormalizeSeverity(string severity)
    {
        return severity.Trim().ToUpperInvariant() switch
        {
            "HIGH" or "CRITICAL" or "SEVERE" => "Critical",
            "MEDIUM" or "WARNING" => "Warning",
            _ => "Info"
        };
    }

    private static int ToInt32Minutes(long minutes)
    {
        if (minutes <= 0)
        {
            return 0;
        }

        return minutes > int.MaxValue ? int.MaxValue : (int)minutes;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    internal sealed record ReplacementRange(
        DateTime StartUtc,
        DateTime EndUtc);
}
