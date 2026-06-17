using DriverTime.Application.Alerts.DTOs;
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
    private const int DriverDownloadIntervalDays = 28;
    private const int VehicleDownloadIntervalDays = 90;
    private const int WarningDays = 7;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ImportRetryOptions _importRetryOptions;

    public AlertsController(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IOptions<ImportRetryOptions> importRetryOptions)
    {
        _dbContext = dbContext;
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

        var nowUtc = DateTime.UtcNow;
        var alerts = new List<AlertDto>();

        alerts.AddRange(await BuildComplianceAlertsAsync(cancellationToken));
        alerts.AddRange(await BuildDriverDownloadAlertsAsync(nowUtc, cancellationToken));
        alerts.AddRange(await BuildVehicleDownloadAlertsAsync(nowUtc, cancellationToken));
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
                    : x.Driver.FirstName + " " + x.Driver.LastName
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
                DueDateUtc = x.ViolationStart,
                CreatedAtUtc = x.CalculatedAt,
                Status = "Open",
                ActionUrl = $"/violations?driverId={x.DriverId}&violationId={x.Id}"
            })
            .ToList();
    }

    private async Task<List<AlertDto>> BuildDriverDownloadAlertsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var drivers = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName,
                x.CardNumber,
                CreatedAtUtc = x.CreatedAtUtc,
                LastDownloadUtc = x.DddFiles
                    .OrderByDescending(file => file.UploadedAtUtc)
                    .Select(file => (DateTime?)file.UploadedAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return drivers
            .Select(x =>
            {
                var nextRequiredDownloadUtc = x.LastDownloadUtc?.AddDays(DriverDownloadIntervalDays);
                var daysUntilDue = GetDaysUntilDue(nextRequiredDownloadUtc, nowUtc);

                if (!IsOverdueOrDue(daysUntilDue))
                {
                    return null;
                }

                var isOverdue = !daysUntilDue.HasValue || daysUntilDue.Value < 0;
                var driverName = $"{x.FirstName} {x.LastName}".Trim();

                return new AlertDto
                {
                    Id = $"driver-download-{x.Id}",
                    Type = isOverdue ? "DriverDownloadOverdue" : "DriverDownloadDue",
                    Category = "Downloads",
                    Severity = isOverdue ? "Critical" : "Warning",
                    Title = isOverdue ? "Odczyt karty kierowcy po terminie" : "Zbliża się termin odczytu karty",
                    Description = x.LastDownloadUtc.HasValue
                        ? "Karta kierowcy wymaga odczytu co 28 dni."
                        : "Brak zapisanego odczytu karty kierowcy.",
                    RelatedEntityType = "Driver",
                    RelatedEntityId = x.Id,
                    RelatedEntityName = string.IsNullOrWhiteSpace(driverName) ? x.CardNumber : driverName,
                    DueDateUtc = nextRequiredDownloadUtc,
                    CreatedAtUtc = x.LastDownloadUtc ?? x.CreatedAtUtc,
                    Status = "Open",
                    ActionUrl = "/downloads"
                };
            })
            .Where(x => x is not null)
            .Cast<AlertDto>()
            .ToList();
    }

    private async Task<List<AlertDto>> BuildVehicleDownloadAlertsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var vehicles = await _dbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.Active)
            .Select(x => new
            {
                x.Id,
                x.RegistrationNumber,
                x.CreatedAt,
                LastDownloadUtc = _dbContext.VehicleUses
                    .Where(vehicleUse =>
                        vehicleUse.DddFile.CompanyId == _currentUser.CompanyId
                        && vehicleUse.RegistrationNumber != null
                        && vehicleUse.RegistrationNumber.Replace(" ", "").Length >= 5
                        && EF.Functions.Like(
                            x.RegistrationNumber.Replace(" ", "").ToUpper(),
                            "%" + vehicleUse.RegistrationNumber.Replace(" ", "").ToUpper()))
                    .OrderByDescending(vehicleUse => vehicleUse.DddFile.UploadedAtUtc)
                    .Select(vehicleUse => (DateTime?)vehicleUse.DddFile.UploadedAtUtc)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return vehicles
            .Select(x =>
            {
                var nextRequiredDownloadUtc = x.LastDownloadUtc?.AddDays(VehicleDownloadIntervalDays);
                var daysUntilDue = GetDaysUntilDue(nextRequiredDownloadUtc, nowUtc);

                if (!IsOverdueOrDue(daysUntilDue))
                {
                    return null;
                }

                var isOverdue = !daysUntilDue.HasValue || daysUntilDue.Value < 0;

                return new AlertDto
                {
                    Id = $"vehicle-download-{x.Id}",
                    Type = isOverdue ? "VehicleDownloadOverdue" : "VehicleDownloadDue",
                    Category = "Downloads",
                    Severity = isOverdue ? "Critical" : "Warning",
                    Title = isOverdue ? "Odczyt tachografu po terminie" : "Zbliża się termin odczytu tachografu",
                    Description = x.LastDownloadUtc.HasValue
                        ? "Tachograf lub pojazd wymaga odczytu co 90 dni."
                        : "Brak zapisanego odczytu tachografu dla pojazdu.",
                    RelatedEntityType = "Vehicle",
                    RelatedEntityId = x.Id,
                    RelatedEntityName = x.RegistrationNumber,
                    DueDateUtc = nextRequiredDownloadUtc,
                    CreatedAtUtc = x.LastDownloadUtc ?? x.CreatedAt,
                    Status = "Open",
                    ActionUrl = "/downloads"
                };
            })
            .Where(x => x is not null)
            .Cast<AlertDto>()
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

    private static bool IsMediumSeverity(string severity)
    {
        return severity.Trim().ToLowerInvariant() is
            "warning" or "medium" or "serious";
    }

    private static int? GetDaysUntilDue(
        DateTime? dueDateUtc,
        DateTime nowUtc)
    {
        if (!dueDateUtc.HasValue)
        {
            return null;
        }

        return (int)Math.Floor((dueDateUtc.Value.Date - nowUtc.Date).TotalDays);
    }

    private static bool IsOverdueOrDue(int? daysUntilDue)
    {
        return !daysUntilDue.HasValue || daysUntilDue.Value <= WarningDays;
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
}
