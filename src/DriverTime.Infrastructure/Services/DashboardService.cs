using DriverTime.Application.Dashboard.DTOs;
using DriverTime.Application.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Violations.DTOs;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IDriverViolationService _driverViolationService;

    public DashboardService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IDriverViolationService driverViolationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _driverViolationService = driverViolationService;
    }

    public async Task<DashboardDto> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        return new DashboardDto
        {
            DddFilesCount = await _dbContext.DddFiles
                .CountAsync(x => x.CompanyId == _currentUser.CompanyId, cancellationToken),
            DriverActivitiesCount = await _dbContext.DriverActivities
                .CountAsync(x => x.DddFile.CompanyId == _currentUser.CompanyId, cancellationToken),
            VehicleUsesCount = await _dbContext.VehicleUses
                .CountAsync(x => x.DddFile.CompanyId == _currentUser.CompanyId, cancellationToken),
            CountryEntriesCount = await _dbContext.CountryEntries
                .CountAsync(x => x.DddFile.CompanyId == _currentUser.CompanyId, cancellationToken),
            GeneratedAtUtc = DateTime.UtcNow
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
        var violations = await _driverViolationService.GetViolationsAsync(
            cancellationToken);
        var violationsByCard = violations
            .Where(x => !string.IsNullOrWhiteSpace(x.DriverCardNumber))
            .GroupBy(x => x.DriverCardNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var driver in drivers)
        {
            var driverViolations = violationsByCard.GetValueOrDefault(driver.CardNumber)
                ?? new List<DriverViolationDto>();
            driver.ViolationsCount = driverViolations.Count;
            driver.SevereViolationsCount = driverViolations.Count(x =>
                x.Severity.Equals("high", StringComparison.OrdinalIgnoreCase)
                || x.Severity.Equals("severe", StringComparison.OrdinalIgnoreCase));
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
}
