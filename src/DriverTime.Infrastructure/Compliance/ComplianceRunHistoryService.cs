using System.Text.Json;
using DriverTime.Application.Compliance;
using DriverTime.Application.Compliance.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Compliance;

public class ComplianceRunHistoryService : IComplianceRunHistoryService
{
    private readonly DriverTimeDbContext _dbContext;

    public ComplianceRunHistoryService(DriverTimeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ComplianceRunDto> SaveRunAsync(
        Guid companyId,
        Guid driverId,
        CompliancePreviewResponseDto preview,
        string trigger,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var run = new ComplianceRun
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DriverId = driverId,
            StartedAtUtc = now,
            FinishedAtUtc = now,
            Trigger = string.IsNullOrWhiteSpace(trigger) ? "manual" : trigger.Trim(),
            TimelineCount = preview.TimelineCount,
            ViolationsCount = preview.ViolationsCount,
            CreatedAtUtc = now,
            Violations = preview.Violations
                .Select(x => MapViolation(x, now))
                .ToList()
        };

        _dbContext.ComplianceRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapRun(run);
    }

    public async Task<IReadOnlyList<ComplianceRunDto>> GetDriverRunsAsync(
        Guid companyId,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var runs = await _dbContext.ComplianceRuns
            .AsNoTracking()
            .Include(x => x.Violations)
            .Where(x => x.CompanyId == companyId && x.DriverId == driverId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return runs.Select(MapRun).ToList();
    }

    public async Task<ComplianceRunDto?> GetRunAsync(
        Guid companyId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _dbContext.ComplianceRuns
            .AsNoTracking()
            .Include(x => x.Violations)
            .FirstOrDefaultAsync(
                x => x.Id == runId && x.CompanyId == companyId,
                cancellationToken);

        return run is null ? null : MapRun(run);
    }

    private static ComplianceRunViolation MapViolation(
        ComplianceViolationPreviewDto violation,
        DateTime createdAtUtc)
    {
        return new ComplianceRunViolation
        {
            Id = Guid.NewGuid(),
            Code = violation.Code,
            RuleName = violation.RuleName,
            Severity = violation.Severity,
            Description = violation.Description,
            PeriodStartUtc = EnsureUtc(violation.PeriodStartUtc),
            PeriodEndUtc = EnsureUtc(violation.PeriodEndUtc),
            ActualMinutes = ToInt32Minutes(violation.ActualMinutes),
            LimitMinutes = ToInt32Minutes(violation.LimitMinutes),
            MetadataJson = JsonSerializer.Serialize(violation.Metadata),
            CreatedAtUtc = createdAtUtc
        };
    }

    private static ComplianceRunDto MapRun(ComplianceRun run)
    {
        return new ComplianceRunDto
        {
            Id = run.Id,
            CompanyId = run.CompanyId,
            DriverId = run.DriverId,
            StartedAtUtc = run.StartedAtUtc,
            FinishedAtUtc = run.FinishedAtUtc,
            Trigger = run.Trigger,
            TimelineCount = run.TimelineCount,
            ViolationsCount = run.ViolationsCount,
            CreatedAtUtc = run.CreatedAtUtc,
            Violations = run.Violations
                .OrderBy(x => x.PeriodStartUtc)
                .Select(MapViolationDto)
                .ToList()
        };
    }

    private static ComplianceRunViolationDto MapViolationDto(
        ComplianceRunViolation violation)
    {
        return new ComplianceRunViolationDto
        {
            Id = violation.Id,
            ComplianceRunId = violation.ComplianceRunId,
            Code = violation.Code,
            RuleName = violation.RuleName,
            Severity = violation.Severity,
            Description = violation.Description,
            PeriodStartUtc = violation.PeriodStartUtc,
            PeriodEndUtc = violation.PeriodEndUtc,
            ActualMinutes = violation.ActualMinutes,
            LimitMinutes = violation.LimitMinutes,
            MetadataJson = violation.MetadataJson,
            CreatedAtUtc = violation.CreatedAtUtc
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
