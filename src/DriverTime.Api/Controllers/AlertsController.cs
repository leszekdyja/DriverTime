using DriverTime.Application.Alerts.DTOs;
using DriverTime.Application.Downloads;
using DriverTime.Application.Downloads.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using DriverTime.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DriverTime.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly IDownloadScheduleService _downloadScheduleService;
    private readonly ICurrentUserService _currentUser;
    private readonly ImportRetryOptions _importRetryOptions;

    public AlertsController(
        DriverTimeDbContext dbContext,
        IDownloadScheduleService downloadScheduleService,
        ICurrentUserService currentUser,
        IOptions<ImportRetryOptions> importRetryOptions)
    {
        _dbContext = dbContext;
        _downloadScheduleService = downloadScheduleService;
        _currentUser = currentUser;
        _importRetryOptions = importRetryOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertDto>>> GetAlerts(
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.CompanyId == Guid.Empty)
        {
            return Unauthorized();
        }

        var alerts = new List<AlertDto>();

        alerts.AddRange(await BuildComplianceAlertsAsync(cancellationToken));
        alerts.AddRange(await BuildDriverDownloadAlertsAsync(cancellationToken));
        alerts.AddRange(await BuildVehicleDownloadAlertsAsync(cancellationToken));
        alerts.AddRange(await BuildImportAlertsAsync(cancellationToken));

        return Ok(alerts
            .OrderBy(x => GetSeverityRank(x.Severity))
            .ThenBy(x => x.DueDateUtc ?? x.CreatedAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToList());
    }

    private async Task<List<AlertDto>> BuildComplianceAlertsAsync(
        CancellationToken cancellationToken)
    {
        var violations = await _dbContext.Violations
            .AsNoTracking()
            .Where(x =>
                x.Driver != null
                && x.Driver.CompanyId == _currentUser.CompanyId
                && (x.Severity.ToLower() == "critical"
                    || x.Severity.ToLower() == "high"
                    || x.Severity.ToLower() == "severe"
                    || x.Severity.ToLower() == "very serious"
                    || x.Severity.ToLower() == "very-serious"
                    || x.Severity.ToLower() == "warning"
                    || x.Severity.ToLower() == "medium"
                    || x.Severity.ToLower() == "serious"))
            .OrderByDescending(x => x.CalculatedAt)
            .Take(200)
            .Select(x => new
            {
                x.Id,
                x.DriverId,
                x.ViolationType,
                x.Severity,
                x.ViolationStart,
                x.CalculatedAt,
                DriverName = x.Driver == null
                    ? string.Empty
                    : FormatDriverName(x.Driver.FirstName, x.Driver.LastName)
            })
            .ToListAsync(cancellationToken);

        return violations
            .Select(x => new AlertDto
            {
                Id = $"compliance-{x.Id}",
                Type = "ComplianceViolation",
                Category = "Compliance",
                Severity = IsHighSeverity(x.Severity) ? "Critical" : "Warning",
                Title = "Naruszenie compliance",
                Description = string.IsNullOrWhiteSpace(x.ViolationType)
                    ? "Wykryto zapisane naruszenie compliance."
                    : x.ViolationType,
                RelatedEntityType = "Driver",
                RelatedEntityId = x.DriverId,
                RelatedEntityName = x.DriverName.Trim(),
                DueDateUtc = null,
                CreatedAtUtc = x.CalculatedAt,
                Status = "Open",
                ActionUrl = $"/violations?driverId={x.DriverId}&violationId={x.Id}"
            })
            .ToList();
    }

    private async Task<List<AlertDto>> BuildDriverDownloadAlertsAsync(
        CancellationToken cancellationToken)
    {
        var drivers = await _downloadScheduleService.GetDriverDownloadsAsync(
            _currentUser.CompanyId,
            cancellationToken);

        return drivers
            .Where(x => x.Status is DownloadStatus.Overdue or DownloadStatus.Warning)
            .Select(x =>
            {
                var isOverdue = x.Status == DownloadStatus.Overdue;
                var driverName = FormatDriverName(x.FirstName, x.LastName);

                return new AlertDto
                {
                    Id = BuildDownloadAlertId(
                        isOverdue ? "driver-download-overdue" : "driver-download-due",
                        x.DriverId,
                        x.NextRequiredDownloadUtc),
                    Type = isOverdue ? "DriverDownloadOverdue" : "DriverDownloadDue",
                    Category = "Downloads",
                    Severity = isOverdue ? "Critical" : "Warning",
                    Title = isOverdue ? "Odczyt karty kierowcy po terminie" : "Zbliża się termin odczytu karty",
                    Description = x.LastDownloadUtc.HasValue
                        ? "Karta kierowcy wymaga odczytu co 28 dni."
                        : "Brak zapisanego odczytu karty kierowcy.",
                    RelatedEntityType = "Driver",
                    RelatedEntityId = x.DriverId,
                    RelatedEntityName = string.IsNullOrWhiteSpace(driverName) ? x.CardNumber : driverName,
                    DueDateUtc = x.NextRequiredDownloadUtc,
                    CreatedAtUtc = x.LastDownloadUtc ?? DateTime.UtcNow,
                    Status = "Open",
                    ActionUrl = "/downloads"
                };
            })
            .ToList();
    }

    private async Task<List<AlertDto>> BuildVehicleDownloadAlertsAsync(
        CancellationToken cancellationToken)
    {
        var vehicles = await _downloadScheduleService.GetVehicleDownloadsAsync(
            _currentUser.CompanyId,
            cancellationToken);

        return vehicles
            .Where(x => x.Status is DownloadStatus.Overdue or DownloadStatus.Warning)
            .Select(x =>
            {
                var isOverdue = x.Status == DownloadStatus.Overdue;

                return new AlertDto
                {
                    Id = BuildDownloadAlertId(
                        isOverdue ? "vehicle-download-overdue" : "vehicle-download-due",
                        x.VehicleId,
                        x.NextRequiredDownloadUtc),
                    Type = isOverdue ? "VehicleDownloadOverdue" : "VehicleDownloadDue",
                    Category = "Downloads",
                    Severity = isOverdue ? "Critical" : "Warning",
                    Title = isOverdue ? "Odczyt tachografu po terminie" : "Zbliża się termin odczytu tachografu",
                    Description = x.LastDownloadUtc.HasValue
                        ? "Tachograf lub pojazd wymaga odczytu co 90 dni."
                        : "Brak zapisanego odczytu tachografu dla pojazdu.",
                    RelatedEntityType = "Vehicle",
                    RelatedEntityId = x.VehicleId,
                    RelatedEntityName = x.RegistrationNumber,
                    DueDateUtc = x.NextRequiredDownloadUtc,
                    CreatedAtUtc = x.LastDownloadUtc ?? DateTime.UtcNow,
                    Status = "Open",
                    ActionUrl = "/downloads"
                };
            })
            .ToList();
    }

    private async Task<List<AlertDto>> BuildImportAlertsAsync(
        CancellationToken cancellationToken)
    {
        var failedImports = await _dbContext.DddImportMonitoringEntries
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == _currentUser.CompanyId
                && x.Status == DddImportMonitoringStatus.Failed)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var alerts = new List<AlertDto>();

        foreach (var import in failedImports)
        {
            alerts.Add(new AlertDto
            {
                Id = $"import-failed-{import.Id}",
                Type = "ImportFailed",
                Category = "Imports",
                Severity = "Critical",
                Title = "Import DDD zakończony błędem",
                Description = string.IsNullOrWhiteSpace(import.LastError)
                    ? import.ErrorMessage
                    : import.LastError,
                RelatedEntityType = "DddImportMonitoring",
                RelatedEntityId = import.Id,
                RelatedEntityName = import.FileName,
                DueDateUtc = import.FinishedAtUtc,
                CreatedAtUtc = import.FinishedAtUtc ?? import.CreatedAtUtc,
                Status = "Open",
                ActionUrl = "/import-monitoring"
            });

            if (import.RetryCount < _importRetryOptions.MaxRetryCount)
            {
                alerts.Add(new AlertDto
                {
                    Id = $"import-retry-{import.Id}",
                    Type = "ImportRetryPending",
                    Category = "Imports",
                    Severity = "Warning",
                    Title = "Import DDD oczekuje na ponowienie",
                    Description = "Nieudany import może zostać ponowiony.",
                    RelatedEntityType = "DddImportMonitoring",
                    RelatedEntityId = import.Id,
                    RelatedEntityName = import.FileName,
                    DueDateUtc = import.LastRetryAtUtc,
                    CreatedAtUtc = import.LastRetryAtUtc ?? import.CreatedAtUtc,
                    Status = "Open",
                    ActionUrl = "/import-monitoring"
                });
            }
        }

        return alerts;
    }

    private static bool IsHighSeverity(string severity)
    {
        return severity.Trim().ToLowerInvariant() is
            "critical" or "high" or "severe" or "very serious" or "very-serious";
    }

    private static string BuildDownloadAlertId(
        string type,
        Guid entityId,
        DateTime? dueDateUtc)
    {
        var dueKey = dueDateUtc.HasValue
            ? dueDateUtc.Value.Date.ToString("yyyyMMdd")
            : "no-download";

        return $"{type}-{entityId}-{dueKey}";
    }

    private static int GetSeverityRank(string severity)
    {
        return severity switch
        {
            "Critical" => 0,
            "Warning" => 1,
            _ => 2
        };
    }

    private static string FormatDriverName(string firstName, string lastName)
    {
        return string.Join(
            " ",
            new[] { lastName, firstName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
    }
}
