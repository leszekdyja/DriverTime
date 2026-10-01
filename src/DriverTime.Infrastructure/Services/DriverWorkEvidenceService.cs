using DriverTime.Application.Drivers;
using DriverTime.Application.Drivers.DTOs;
using DriverTime.Application.Interfaces;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class DriverWorkEvidenceService : IDriverWorkEvidenceService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DriverWorkEvidenceService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<DriverWorkEvidenceMonthDto?> GetMonthAsync(
        Guid driverId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsMobileDriver && driverId != _currentUser.DriverId)
        {
            return null;
        }

        ValidateMonth(year, month);

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

        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var startDateTime = firstDay.ToDateTime(TimeOnly.MinValue);
        var endDateTime = firstDay.AddMonths(1).ToDateTime(TimeOnly.MinValue);

        var entries = await _dbContext.DriverWorkEvidenceEntries
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == _currentUser.CompanyId
                && x.DriverId == driverId
                && x.StartDateTime < endDateTime
                && x.EndDateTime > startDateTime)
            .OrderBy(x => x.StartDateTime)
            .Select(x => new DriverWorkEvidenceEntryDto
            {
                Id = x.Id,
                Date = x.Date,
                StartDateTime = x.StartDateTime,
                EndDateTime = x.EndDateTime,
                StartTime = TimeOnly.FromDateTime(x.StartDateTime),
                EndTime = TimeOnly.FromDateTime(x.EndDateTime),
                EndsNextDay = x.EndDateTime.Date > x.StartDateTime.Date,
                ActivityType = x.ActivityType,
                Source = x.Source,
                VehicleRegistration = x.VehicleRegistration,
                CountryCode = x.CountryCode,
                DistanceKm = x.DistanceKm,
                Description = x.Description,
                DurationMinutes = GetDurationMinutes(x.StartDateTime, x.EndDateTime)
            })
            .ToListAsync(cancellationToken);

        var result = new DriverWorkEvidenceMonthDto
        {
            DriverId = driver.Id,
            DriverFullName = $"{driver.FirstName} {driver.LastName}".Trim(),
            Year = year,
            Month = month
        };

        for (var date = firstDay; date <= lastDay; date = date.AddDays(1))
        {
            var day = BuildDay(date, entries.Where(x => x.Date == date).ToList());
            result.Days.Add(day);
            AddToSummary(result.Summary, day);
        }

        return result;
    }

    public async Task<DriverWorkEvidenceEntryDto?> CreateEntryAsync(
        Guid driverId,
        DriverWorkEvidenceEntryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.IsMobileDriver && driverId != _currentUser.DriverId)
        {
            return null;
        }

        var driverExists = await _dbContext.Drivers
            .AnyAsync(x => x.Id == driverId && x.CompanyId == _currentUser.CompanyId, cancellationToken);

        if (!driverExists)
        {
            return null;
        }

        var period = BuildPeriod(request);
        await ValidateAsync(driverId, null, request, period, cancellationToken);

        var now = DateTime.UtcNow;
        var entry = new DriverWorkEvidenceEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            DriverId = driverId,
            Date = request.Date,
            StartDateTime = period.Start,
            EndDateTime = period.End,
            ActivityType = request.ActivityType,
            Source = request.Source ?? DriverWorkEvidenceEntrySource.Manual,
            VehicleRegistration = NormalizeText(request.VehicleRegistration),
            CountryCode = NormalizeText(request.CountryCode)?.ToUpperInvariant(),
            DistanceKm = request.DistanceKm,
            Description = NormalizeText(request.Description),
            CreatedAtUtc = now
        };

        _dbContext.DriverWorkEvidenceEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(entry);
    }

    public async Task<DriverWorkEvidenceEntryDto?> UpdateEntryAsync(
        Guid entryId,
        DriverWorkEvidenceEntryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.DriverWorkEvidenceEntries
            .FirstOrDefaultAsync(x => x.Id == entryId && x.CompanyId == _currentUser.CompanyId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        if (_currentUser.IsMobileDriver && entry.DriverId != _currentUser.DriverId)
        {
            return null;
        }

        var period = BuildPeriod(request);
        await ValidateAsync(entry.DriverId, entry.Id, request, period, cancellationToken);

        entry.Date = request.Date;
        entry.StartDateTime = period.Start;
        entry.EndDateTime = period.End;
        entry.ActivityType = request.ActivityType;
        entry.VehicleRegistration = NormalizeText(request.VehicleRegistration);
        entry.CountryCode = NormalizeText(request.CountryCode)?.ToUpperInvariant();
        entry.DistanceKm = request.DistanceKm;
        entry.Description = NormalizeText(request.Description);
        entry.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(entry);
    }

    public async Task<bool> DeleteEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.DriverWorkEvidenceEntries
            .FirstOrDefaultAsync(x => x.Id == entryId && x.CompanyId == _currentUser.CompanyId, cancellationToken);

        if (entry is null)
        {
            return false;
        }

        if (_currentUser.IsMobileDriver && entry.DriverId != _currentUser.DriverId)
        {
            return false;
        }

        _dbContext.DriverWorkEvidenceEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public static (DateTime Start, DateTime End) BuildPeriod(DriverWorkEvidenceEntryRequestDto request)
    {
        var start = request.Date.ToDateTime(request.StartTime);
        var endDate = request.EndsNextDay || request.EndTime <= request.StartTime
            ? request.Date.AddDays(1)
            : request.Date;
        var end = endDate.ToDateTime(request.EndTime);

        return (start, end);
    }

    public static int GetDurationMinutes(DateTime start, DateTime end)
    {
        var minutes = (int)Math.Round((end - start).TotalMinutes, MidpointRounding.AwayFromZero);

        return Math.Max(0, minutes);
    }

    private async Task ValidateAsync(
        Guid driverId,
        Guid? editedEntryId,
        DriverWorkEvidenceEntryRequestDto request,
        (DateTime Start, DateTime End) period,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (!Enum.IsDefined(request.ActivityType))
        {
            errors.Add("Nieprawidłowy typ aktywności.");
        }

        if (request.Source.HasValue && !Enum.IsDefined(request.Source.Value))
        {
            errors.Add("Nieprawidłowe źródło wpisu ewidencji.");
        }

        if (period.End <= period.Start)
        {
            errors.Add("Godzina zakończenia musi być późniejsza niż godzina rozpoczęcia.");
        }

        if (GetDurationMinutes(period.Start, period.End) > 24 * 60)
        {
            errors.Add("Pojedynczy wpis ewidencji nie może być dłuższy niż 24 godziny.");
        }

        if (request.DistanceKm < 0)
        {
            errors.Add("Dystans nie może być ujemny.");
        }

        if (!string.IsNullOrWhiteSpace(request.CountryCode) && request.CountryCode.Trim().Length > 10)
        {
            errors.Add("Kod kraju może mieć maksymalnie 10 znaków.");
        }

        if (!string.IsNullOrWhiteSpace(request.VehicleRegistration) && request.VehicleRegistration.Trim().Length > 50)
        {
            errors.Add("Numer rejestracyjny może mieć maksymalnie 50 znaków.");
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > 2000)
        {
            errors.Add("Opis może mieć maksymalnie 2000 znaków.");
        }

        if (errors.Count == 0)
        {
            var overlaps = await _dbContext.DriverWorkEvidenceEntries
                .AsNoTracking()
                .AnyAsync(x =>
                    x.CompanyId == _currentUser.CompanyId
                    && x.DriverId == driverId
                    && (!editedEntryId.HasValue || x.Id != editedEntryId.Value)
                    && x.StartDateTime < period.End
                    && x.EndDateTime > period.Start,
                    cancellationToken);

            if (overlaps)
            {
                errors.Add("Wpis ewidencji nachodzi na istniejącą aktywność kierowcy.");
            }
        }

        if (errors.Count > 0)
        {
            throw new DriverWorkEvidenceValidationException(errors);
        }
    }

    private static DriverWorkEvidenceDayDto BuildDay(
        DateOnly date,
        List<DriverWorkEvidenceEntryDto> entries)
    {
        var day = new DriverWorkEvidenceDayDto
        {
            Date = date,
            Entries = entries
        };

        foreach (var entry in entries)
        {
            AddDuration(day, entry.ActivityType, entry.DurationMinutes);
        }

        return day;
    }

    private static void AddDuration(
        DriverWorkEvidenceDayDto day,
        DriverWorkEvidenceActivityType activityType,
        int durationMinutes)
    {
        day.TotalTrackedMinutes += durationMinutes;

        switch (activityType)
        {
            case DriverWorkEvidenceActivityType.Driving:
                day.DrivingMinutes += durationMinutes;
                break;
            case DriverWorkEvidenceActivityType.OtherWork:
                day.OtherWorkMinutes += durationMinutes;
                break;
            case DriverWorkEvidenceActivityType.Availability:
                day.AvailabilityMinutes += durationMinutes;
                break;
            case DriverWorkEvidenceActivityType.BreakRest:
                day.BreakRestMinutes += durationMinutes;
                break;
            case DriverWorkEvidenceActivityType.Vacation:
            case DriverWorkEvidenceActivityType.SickLeave:
            case DriverWorkEvidenceActivityType.DayOff:
            case DriverWorkEvidenceActivityType.OtherAbsence:
                day.AbsenceMinutes += durationMinutes;
                break;
        }
    }

    private static void AddToSummary(
        DriverWorkEvidenceSummaryDto summary,
        DriverWorkEvidenceDayDto day)
    {
        summary.DrivingMinutes += day.DrivingMinutes;
        summary.OtherWorkMinutes += day.OtherWorkMinutes;
        summary.AvailabilityMinutes += day.AvailabilityMinutes;
        summary.BreakRestMinutes += day.BreakRestMinutes;
        summary.AbsenceMinutes += day.AbsenceMinutes;
        summary.TotalTrackedMinutes += day.TotalTrackedMinutes;
    }

    private static DriverWorkEvidenceEntryDto Map(DriverWorkEvidenceEntry entry)
    {
        return new DriverWorkEvidenceEntryDto
        {
            Id = entry.Id,
            Date = entry.Date,
            StartDateTime = entry.StartDateTime,
            EndDateTime = entry.EndDateTime,
            StartTime = TimeOnly.FromDateTime(entry.StartDateTime),
            EndTime = TimeOnly.FromDateTime(entry.EndDateTime),
            EndsNextDay = entry.EndDateTime.Date > entry.StartDateTime.Date,
            ActivityType = entry.ActivityType,
            Source = entry.Source,
            VehicleRegistration = entry.VehicleRegistration,
            CountryCode = entry.CountryCode,
            DistanceKm = entry.DistanceKm,
            Description = entry.Description,
            DurationMinutes = GetDurationMinutes(entry.StartDateTime, entry.EndDateTime)
        };
    }

    private static void ValidateMonth(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
        {
            throw new DriverWorkEvidenceValidationException(["Nieprawidłowy miesiąc ewidencji."]);
        }
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();

        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
