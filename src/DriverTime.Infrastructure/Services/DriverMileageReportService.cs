using System.Linq.Expressions;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Reports.DTOs;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverMileageReportService : IDriverMileageReportService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverMileageReportService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DriverMileageReportDto?> GetReportAsync(
        Guid driverId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var driver = await _dbContext.Drivers
            .AsNoTracking()
            .Where(x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId)
            .Select(x => new
            {
                x.Id,
                x.FirstName,
                x.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (driver is null)
        {
            return null;
        }

        var fromUtc = ToUtcStart(from);
        var toUtcExclusive = ToUtcExclusive(to);
        var scope = BuildVehicleUseScope(driverId, _currentUser.CompanyId, fromUtc, toUtcExclusive);
        var rows = await _dbContext.VehicleUses
            .AsNoTracking()
            .Where(scope)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .Select(x => new DriverMileageReportRowSource
            {
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                RegistrationNumber = x.RegistrationNumber,
                StartOdometerKm = x.StartOdometerKm,
                EndOdometerKm = x.EndOdometerKm,
                DistanceKm = x.DistanceKm
            })
            .ToListAsync(cancellationToken);

        return BuildReport(
            driver.Id,
            FormatDriverName(driver.LastName, driver.FirstName),
            from,
            to,
            rows);
    }

    internal static Expression<Func<VehicleUse, bool>> BuildVehicleUseScope(
        Guid driverId,
        Guid companyId,
        DateTime fromUtc,
        DateTime toUtcExclusive)
    {
        return x =>
            x.DddFile.CompanyId == companyId
            && x.DddFile.DriverId == driverId
            && x.StartUtc < toUtcExclusive
            && x.EndUtc > fromUtc;
    }

    internal static DriverMileageReportDto BuildReport(
        Guid driverId,
        string driverName,
        DateOnly from,
        DateOnly to,
        IEnumerable<DriverMileageReportRowSource> sources)
    {
        var rows = sources
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .Select(x => new DriverMileageReportRowDto
            {
                Date = DateOnly.FromDateTime(x.StartUtc),
                StartUtc = x.StartUtc,
                EndUtc = x.EndUtc,
                RegistrationNumber = string.IsNullOrWhiteSpace(x.RegistrationNumber)
                    ? "Brak danych"
                    : x.RegistrationNumber.Trim(),
                StartOdometerKm = x.StartOdometerKm,
                EndOdometerKm = x.EndOdometerKm,
                DistanceKm = x.DistanceKm,
                HasDistanceData = x.DistanceKm.HasValue
            })
            .ToList();

        return new DriverMileageReportDto
        {
            DriverId = driverId,
            DriverName = driverName,
            From = from,
            To = to,
            TotalDistanceKm = rows
                .Where(x => x.DistanceKm.HasValue)
                .Sum(x => x.DistanceKm!.Value),
            VehicleUseCount = rows.Count,
            MissingDistanceCount = rows.Count(x => !x.DistanceKm.HasValue),
            Rows = rows
        };
    }

    private static DateTime ToUtcStart(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime ToUtcExclusive(DateOnly date) =>
        DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static string FormatDriverName(string lastName, string firstName) =>
        $"{lastName} {firstName}".Trim();

    internal sealed class DriverMileageReportRowSource
    {
        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public string RegistrationNumber { get; set; } = string.Empty;

        public int? StartOdometerKm { get; set; }

        public int? EndOdometerKm { get; set; }

        public int? DistanceKm { get; set; }
    }
}
