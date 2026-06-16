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
        "EU561_BIWEEKLY_DRIVING_90H"
    ];

    private readonly DriverTimeDbContext _dbContext;
    private readonly IComplianceEngineService _complianceEngineService;
    private readonly ILogger<ComplianceEvaluationService> _logger;

    public ComplianceEvaluationService(
        DriverTimeDbContext dbContext,
        IComplianceEngineService complianceEngineService,
        ILogger<ComplianceEvaluationService> logger)
    {
        _dbContext = dbContext;
        _complianceEngineService = complianceEngineService;
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
            cancellationToken);

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

        var deletedCount = await _dbContext.Violations
            .Where(x =>
                x.DriverId == driverId &&
                replaceableCodes.Contains(x.RegulationReference))
            .ExecuteDeleteAsync(cancellationToken);

        if (preview.Violations.Count == 0)
        {
            _logger.LogInformation(
                "Compliance evaluation completed for driver {DriverId}. No violations detected. CompanyId={CompanyId}, Timeline={TimelineCount}, DeletedExisting={DeletedCount}, ReplaceableCodes={ReplaceableCodes}.",
                driverId,
                companyId,
                preview.Timeline.Count,
                deletedCount,
                string.Join(", ", replaceableCodes));

            return 0;
        }

        var calculatedAt = DateTime.UtcNow;
        var violations = preview.Violations
            .Select(x => MapViolation(driverId, x, calculatedAt))
            .ToList();

        _dbContext.Violations.AddRange(violations);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Compliance evaluation completed for driver {DriverId}. CompanyId={CompanyId}, Timeline={TimelineCount}, DeletedExisting={DeletedCount}, Saved violations: {Count}.",
            driverId,
            companyId,
            preview.Timeline.Count,
            deletedCount,
            violations.Count);

        return violations.Count;
    }

    private static Violation MapViolation(
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
}
