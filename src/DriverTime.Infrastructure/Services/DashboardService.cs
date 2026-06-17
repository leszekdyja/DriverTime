using DriverTime.Application.Dashboard.DTOs;
using DriverTime.Application.Downloads;
using DriverTime.Application.Downloads.DTOs;
using DriverTime.Application.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverTime.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDownloadScheduleService _downloadScheduleService;
    private readonly ComplianceSchedulerOptions _schedulerOptions;

    public DashboardService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IDownloadScheduleService downloadScheduleService,
        IOptions<ComplianceSchedulerOptions> schedulerOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _downloadScheduleService = downloadScheduleService;
        _schedulerOptions = schedulerOptions.Value;
    }

    public async Task<DashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var companyId = _currentUser.CompanyId;
        var driverDownloads = await _downloadScheduleService.GetDriverDownloadsAsync(companyId, cancellationToken);
        var vehicleDownloads = await _downloadScheduleService.GetVehicleDownloadsAsync(companyId, cancellationToken);

        var highSeverityDrivers = await _dbContext.Violations
            .AsNoTracking()
            .Where(x =>
                x.Driver != null
                && x.Driver.CompanyId == companyId
                && (x.Severity.ToLower() == "critical"
                    || x.Severity.ToLower() == "high"
                    || x.Severity.ToLower() == "severe"
                    || x.Severity.ToLower() == "very serious"
                    || x.Severity.ToLower() == "very-serious"))
            .Select(x => x.DriverId)
            .Distinct()
            .CountAsync(cancellationToken);

        var mediumSeverityDrivers = await _dbContext.Violations
            .AsNoTracking()
            .Where(x =>
                x.Driver != null
                && x.Driver.CompanyId == companyId
                && (x.Severity.ToLower() == "warning"
                    || x.Severity.ToLower() == "medium"
                    || x.Severity.ToLower() == "serious"))
            .Select(x => x.DriverId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new DashboardDto
        {
            DddFilesCount = await _dbContext.DddFiles
                .CountAsync(x => x.CompanyId == companyId, cancellationToken),
            DriverActivitiesCount = await _dbContext.DriverActivities
                .CountAsync(x => x.DddFile.CompanyId == companyId, cancellationToken),
            VehicleUsesCount = await _dbContext.VehicleUses
                .CountAsync(x => x.DddFile.CompanyId == companyId, cancellationToken),
            CountryEntriesCount = await _dbContext.CountryEntries
                .CountAsync(x => x.DddFile.CompanyId == companyId, cancellationToken),
            OverdueDriverDownloads = driverDownloads.Count(x => x.Status == DownloadStatus.Overdue),
            DriverDownloadsDueIn7Days = CountDownloadsDueInDays(driverDownloads, 7),
            DownloadsDueIn7Days = CountDownloadsDueInDays(driverDownloads, 7) + CountDownloadsDueInDays(vehicleDownloads, 7),
            DriverDownloadsDueIn14Days = CountDownloadsDueInDays(driverDownloads, 14),
            OverdueVehicleDownloads = vehicleDownloads.Count(x => x.Status == DownloadStatus.Overdue),
            VehicleDownloadsDueIn7Days = CountDownloadsDueInDays(vehicleDownloads, 7),
            VehicleDownloadsDueIn14Days = CountDownloadsDueInDays(vehicleDownloads, 14),
            DriversWithHighViolations = highSeverityDrivers,
            DriversWithMediumViolations = mediumSeverityDrivers,
            GeneratedAtUtc = now
        };
    }

    public async Task<DriverRiskOverviewDto> GetRiskOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .Select(x => new DriverRiskDto
            {
                DriverId = x.Id,
                FirstName = x.FirstName,
                LastName = x.LastName,
                CardNumber = x.CardNumber,
                LastImportAtUtc = x.DddFiles
                    .Select(file => (DateTime?)file.UploadedAtUtc)
                    .Max(),
                LastActivityAtUtc = x.DddFiles
                    .SelectMany(file => file.DriverActivities)
                    .Select(activity => (DateTime?)activity.EndUtc)
                    .Max()
            })
            .ToListAsync(cancellationToken);

        var violationSummaries = await _dbContext.Violations
            .AsNoTracking()
            .Where(x => x.Driver != null && x.Driver.CompanyId == _currentUser.CompanyId)
            .GroupBy(x => x.DriverId)
            .Select(x => new
            {
                DriverId = x.Key,
                ViolationsCount = x.Count(),
                SevereViolationsCount = x.Count(violation =>
                    violation.Severity.ToLower() == "critical" ||
                    violation.Severity.ToLower() == "high" ||
                    violation.Severity.ToLower() == "severe" ||
                    violation.Severity.ToLower() == "very serious" ||
                    violation.Severity.ToLower() == "very-serious")
            })
            .ToDictionaryAsync(x => x.DriverId, cancellationToken);

        foreach (var driver in drivers)
        {
            if (violationSummaries.TryGetValue(driver.DriverId, out var violationSummary))
            {
                driver.ViolationsCount = violationSummary.ViolationsCount;
                driver.SevereViolationsCount = violationSummary.SevereViolationsCount;
            }

            driver.DaysSinceLastImport = GetDaysSince(driver.LastImportAtUtc, now);
            driver.DaysSinceLastActivity = GetDaysSince(driver.LastActivityAtUtc, now);
            driver.RiskScore = CalculateRiskScore(driver);
            driver.RiskStatus = GetRiskStatus(driver.RiskScore);
        }

        var orderedDrivers = drivers
            .OrderByDescending(x => x.RiskScore)
            .ThenByDescending(x => x.SevereViolationsCount)
            .ThenBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToList();

        return new DriverRiskOverviewDto
        {
            GeneratedAtUtc = now,
            LowRiskCount = orderedDrivers.Count(x => x.RiskStatus == "Low"),
            MediumRiskCount = orderedDrivers.Count(x => x.RiskStatus == "Medium"),
            HighRiskCount = orderedDrivers.Count(x => x.RiskStatus == "High"),
            CriticalRiskCount = orderedDrivers.Count(x => x.RiskStatus == "Critical"),
            Drivers = orderedDrivers
        };
    }

    private static int? GetDaysSince(DateTime? value, DateTime now) =>
        value.HasValue ? Math.Max((now.Date - value.Value.Date).Days, 0) : null;

    private static int CountDownloadsDueInDays<TDownload>(
        IReadOnlyList<TDownload> downloads,
        int days)
        where TDownload : class
    {
        return downloads.Count(x =>
            GetDownloadStatus(x) != DownloadStatus.Overdue
            && GetDownloadDaysUntilDue(x).HasValue
            && GetDownloadDaysUntilDue(x)!.Value <= days);
    }

    private static string GetDownloadStatus<TDownload>(TDownload download)
    {
        return download switch
        {
            DriverDownloadDto driver => driver.Status,
            VehicleDownloadDto vehicle => vehicle.Status,
            _ => DownloadStatus.Ok
        };
    }

    private static int? GetDownloadDaysUntilDue<TDownload>(TDownload download)
    {
        return download switch
        {
            DriverDownloadDto driver => driver.DaysUntilDue,
            VehicleDownloadDto vehicle => vehicle.DaysUntilDue,
            _ => null
        };
    }

    private static int CalculateRiskScore(DriverRiskDto driver)
    {
        var score = driver.ViolationsCount + driver.SevereViolationsCount * 3;

        score += driver.DaysSinceLastImport switch
        {
            null => 4,
            > 30 => 4,
            > 14 => 2,
            _ => 0
        };
        score += driver.DaysSinceLastActivity switch
        {
            null => 4,
            > 14 => 4,
            > 7 => 2,
            _ => 0
        };

        return score;
    }

    private static string GetRiskStatus(int score) => score switch
    {
        >= 12 => "Critical",
        >= 8 => "High",
        >= 4 => "Medium",
        _ => "Low"
    };

    public async Task<ComplianceRunDashboardStatsDto> GetComplianceRunStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var recentRuns = await _dbContext.ComplianceRuns
            .AsNoTracking()
            .Include(x => x.Violations)
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var lastRun = recentRuns.FirstOrDefault();
        var lastSchedulerRun = recentRuns.FirstOrDefault(x =>
            x.Trigger.Equals("Scheduler", StringComparison.OrdinalIgnoreCase));

        var lastRunGroup = lastRun is null
            ? new List<ComplianceRun>()
            : recentRuns
                .Where(x =>
                    x.Trigger.Equals(lastRun.Trigger, StringComparison.OrdinalIgnoreCase) &&
                    x.CreatedAtUtc >= lastRun.CreatedAtUtc.AddMinutes(-1) &&
                    x.CreatedAtUtc <= lastRun.CreatedAtUtc.AddMinutes(1))
                .ToList();

        return new ComplianceRunDashboardStatsDto
        {
            GeneratedAtUtc = now,
            RecentRunsCount = recentRuns.Count,
            LastStatus = GetRunStatus(lastRun),
            LastRunAtUtc = lastRun?.CreatedAtUtc,
            LastRunViolationsCount = lastRun?.ViolationsCount ?? 0,
            HighViolationsCount = CountSeverity(lastRun, "high"),
            MediumViolationsCount = CountSeverity(lastRun, "medium"),
            LowViolationsCount = CountSeverity(lastRun, "low"),
            DriversInLastRunCount = lastRunGroup
                .Select(x => x.DriverId)
                .Distinct()
                .Count(),
            SchedulerEnabled = _schedulerOptions.Enabled,
            LastSchedulerRunAtUtc = lastSchedulerRun?.CreatedAtUtc,
            LastSchedulerStatus = GetRunStatus(lastSchedulerRun),
            LastSchedulerViolationsCount = lastSchedulerRun?.ViolationsCount ?? 0
        };
    }

    private static string GetRunStatus(ComplianceRun? run)
    {
        if (run is null)
        {
            return "NoData";
        }

        return run.FinishedAtUtc.HasValue ? "Completed" : "Running";
    }

    private static int CountSeverity(
        ComplianceRun? run,
        string severityGroup)
    {
        if (run is null)
        {
            return 0;
        }

        return run.Violations.Count(x => GetSeverityGroup(x.Severity) == severityGroup);
    }

    private static string GetSeverityGroup(string severity)
    {
        return severity.Trim().ToLowerInvariant() switch
        {
            "critical" or "high" or "severe" or "very serious" or "very-serious" => "high",
            "warning" or "medium" or "serious" => "medium",
            _ => "low"
        };
    }
}
