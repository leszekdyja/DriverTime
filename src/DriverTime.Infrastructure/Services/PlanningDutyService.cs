using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningDutyService : IPlanningDutyService
{
    private const int DutyNumberMaxLength = 50;
    private const int NameMaxLength = 200;
    private const int VehicleRequirementMaxLength = 200;
    private const int NotesMaxLength = 4000;
    private const int SourceFileNameMaxLength = 500;
    private const int LineCodeMaxLength = 50;
    private const int VariantMaxLength = 100;
    private const int StopNameMaxLength = 200;
    private const int TripGroupMaxLength = 100;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PlanningDutyService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<PlanningDutyListDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;

        return await _dbContext.PlanningDuties
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.ValidFrom)
            .ThenBy(x => x.DutyNumber)
            .Select(x => ToListDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningDutyDetailsDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var duty = await GetDutyInCurrentCompany(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return duty is null ? null : ToDetailsDto(duty);
    }

    public async Task<PlanningDutyDetailsDto> CreateAsync(
        CreatePlanningDutyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var now = DateTime.UtcNow;
        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            DutyNumber = request.DutyNumber.Trim(),
            Name = request.Name.Trim(),
            CreatedAtUtc = now,
            CreatedAt = now
        };

        ApplyRequest(duty, request, now);

        _dbContext.PlanningDuties.Add(duty);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailsDto(duty);
    }

    public async Task<PlanningDutyDetailsDto?> UpdateAsync(
        Guid id,
        UpdatePlanningDutyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var duty = await GetDutyInCurrentCompany(id)
            .FirstOrDefaultAsync(cancellationToken);

        if (duty is null)
        {
            return null;
        }

        ApplyRequest(duty, request, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDetailsDto(duty);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var duty = await GetDutyInCurrentCompany(id)
            .FirstOrDefaultAsync(cancellationToken);

        if (duty is null)
        {
            return false;
        }

        _dbContext.PlanningDuties.Remove(duty);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }


    public async Task<PlanningDutyPdfImportConfirmResultDto> ConfirmPdfImportAsync(
        PlanningDutyPdfImportConfirmRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateConfirmRequest(request);

        var companyId = _currentUser.CompanyId;
        var now = DateTime.UtcNow;
        var dutyNumbers = request.Duties
            .Select(x => NormalizeRequired(x.DutyNumber))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingDuties = await _dbContext.PlanningDuties
            .Include(x => x.Lines)
            .Include(x => x.Stops)
            .Where(x => x.CompanyId == companyId && dutyNumbers.Contains(x.DutyNumber))
            .ToListAsync(cancellationToken);

        var result = ConfirmImportForCompany(existingDuties, request, companyId, now);

        foreach (var duty in existingDuties.Where(x => _dbContext.Entry(x).State == EntityState.Detached))
        {
            _dbContext.PlanningDuties.Add(duty);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    internal static PlanningDutyPdfImportConfirmResultDto ConfirmImportForCompany(
        IList<PlanningDuty> existingDuties,
        PlanningDutyPdfImportConfirmRequestDto request,
        Guid companyId,
        DateTime now)
    {
        ValidateConfirmRequest(request);

        var result = new PlanningDutyPdfImportConfirmResultDto();

        foreach (var item in request.Duties)
        {
            var dutyNumber = NormalizeRequired(item.DutyNumber);
            var lineKey = GetLineKey(item.Line);
            var matchingDuty = existingDuties.FirstOrDefault(duty =>
                duty.CompanyId == companyId
                && string.Equals(duty.DutyNumber, dutyNumber, StringComparison.OrdinalIgnoreCase)
                && duty.StartTime == item.StartTime
                && duty.EndTime == item.EndTime
                && string.Equals(GetLineKey(duty), lineKey, StringComparison.OrdinalIgnoreCase));

            if (matchingDuty is null)
            {
                var created = CreateDutyFromConfirmItem(item, request.SourceFileName, companyId, now);
                existingDuties.Add(created);
                result.CreatedCount++;
                result.Items.Add(CreateResultItem(item, "Created", "Dodano nową służbę."));
                continue;
            }

            if (IsConfirmItemIdentical(matchingDuty, item, request.SourceFileName))
            {
                result.UnchangedCount++;
                result.Items.Add(CreateResultItem(item, "Unchanged", "Służba bez zmian."));
                continue;
            }

            ApplyConfirmItem(matchingDuty, item, request.SourceFileName, now, setUpdatedAt: true);
            result.UpdatedCount++;
            result.Items.Add(CreateResultItem(item, "Updated", "Zaktualizowano istniejącą służbę."));
        }

        return result;
    }
    internal static void Validate(CreatePlanningDutyRequest request)
    {
        var errors = new List<string>();

        RequireText(request.DutyNumber, "Numer służby jest wymagany.", DutyNumberMaxLength, "Numer służby jest za długi.", errors);
        RequireText(request.Name, "Nazwa jest wymagana.", NameMaxLength, "Nazwa jest za długa.", errors);
        OptionalText(request.VehicleRequirement, VehicleRequirementMaxLength, "Wymagany pojazd jest za długi.", errors);
        OptionalText(request.Notes, NotesMaxLength, "Uwagi są za długie.", errors);
        OptionalText(request.SourceFileName, SourceFileNameMaxLength, "Nazwa pliku źródłowego jest za długa.", errors);

        NonNegative(request.TotalDurationMinutes, "Czas całkowity nie może być ujemny.", errors);
        NonNegative(request.WorkMinutes, "Czas pracy nie może być ujemny.", errors);
        NonNegative(request.BreakMinutes, "Czas przerw nie może być ujemny.", errors);
        NonNegative(request.DrivingMinutes, "Czas jazdy nie może być ujemny.", errors);
        NonNegative(request.DistanceKm, "Kilometry nie mogą być ujemne.", errors);

        foreach (var line in request.Lines)
        {
            RequireText(line.LineCode, "Kod linii jest wymagany.", LineCodeMaxLength, "Kod linii jest za długi.", errors);
            OptionalText(line.Variant, VariantMaxLength, "Wariant linii jest za długi.", errors);
            NonNegative(line.DistanceKm, "Kilometry linii nie mogą być ujemne.", errors);
        }

        foreach (var stop in request.Stops)
        {
            RequireText(stop.StopName, "Nazwa przystanku jest wymagana.", StopNameMaxLength, "Nazwa przystanku jest za długa.", errors);
            OptionalText(stop.TripGroup, TripGroupMaxLength, "Grupa kursu jest za długa.", errors);
            OptionalText(stop.LineCode, LineCodeMaxLength, "Kod linii przystanku jest za długi.", errors);
            NonNegative(stop.Sequence, "Kolejność przystanku nie może być ujemna.", errors);
            NonNegative(stop.Km, "Kilometry przystanku nie mogą być ujemne.", errors);
        }

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }

    internal static PlanningDuty CreateDutyForCompany(CreatePlanningDutyRequest request, Guid companyId, DateTime now)
    {
        Validate(request);

        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DutyNumber = request.DutyNumber.Trim(),
            Name = request.Name.Trim(),
            CreatedAtUtc = now,
            CreatedAt = now
        };

        ApplyRequest(duty, request, now, setUpdatedAt: false);

        return duty;
    }

    internal static bool IsInCompanyScope(PlanningDuty duty, Guid companyId) =>
        duty.CompanyId == companyId;

    private IQueryable<PlanningDuty> GetDutyInCurrentCompany(Guid id)
    {
        var companyId = _currentUser.CompanyId;

        return _dbContext.PlanningDuties
            .Include(x => x.Lines)
            .Include(x => x.Stops)
            .Where(x => x.Id == id && x.CompanyId == companyId);
    }

    private static void ApplyRequest(
        PlanningDuty duty,
        CreatePlanningDutyRequest request,
        DateTime now,
        bool setUpdatedAt = true)
    {
        duty.DutyNumber = request.DutyNumber.Trim();
        duty.Name = request.Name.Trim();
        duty.ValidFrom = request.ValidFrom;
        duty.VehicleRequirement = NormalizeOptional(request.VehicleRequirement);
        duty.StartTime = request.StartTime;
        duty.EndTime = request.EndTime;
        duty.TotalDurationMinutes = request.TotalDurationMinutes;
        duty.WorkMinutes = request.WorkMinutes;
        duty.BreakMinutes = request.BreakMinutes;
        duty.DrivingMinutes = request.DrivingMinutes;
        duty.DistanceKm = request.DistanceKm;
        duty.Notes = NormalizeOptional(request.Notes);
        duty.SourceFileName = NormalizeOptional(request.SourceFileName);

        if (setUpdatedAt)
        {
            duty.UpdatedAtUtc = now;
        }

        duty.Lines.Clear();
        foreach (var line in request.Lines)
        {
            duty.Lines.Add(new PlanningDutyLine
            {
                Id = line.Id == Guid.Empty ? Guid.NewGuid() : line.Id,
                PlanningDutyId = duty.Id,
                LineCode = line.LineCode.Trim(),
                Variant = NormalizeOptional(line.Variant),
                DistanceKm = line.DistanceKm
            });
        }

        duty.Stops.Clear();
        foreach (var stop in request.Stops.OrderBy(x => x.Sequence))
        {
            duty.Stops.Add(new PlanningDutyStop
            {
                Id = stop.Id == Guid.Empty ? Guid.NewGuid() : stop.Id,
                PlanningDutyId = duty.Id,
                Sequence = stop.Sequence,
                StopName = stop.StopName.Trim(),
                Km = stop.Km,
                TripGroup = NormalizeOptional(stop.TripGroup),
                ArrivalTime = stop.ArrivalTime,
                DepartureTime = stop.DepartureTime,
                LineCode = NormalizeOptional(stop.LineCode)
            });
        }
    }

    private static PlanningDutyListDto ToListDto(PlanningDuty duty) => new()
    {
        Id = duty.Id,
        DutyNumber = duty.DutyNumber,
        Name = duty.Name,
        ValidFrom = duty.ValidFrom,
        VehicleRequirement = duty.VehicleRequirement,
        StartTime = duty.StartTime,
        EndTime = duty.EndTime,
        TotalDurationMinutes = duty.TotalDurationMinutes,
        WorkMinutes = duty.WorkMinutes,
        BreakMinutes = duty.BreakMinutes,
        DrivingMinutes = duty.DrivingMinutes,
        DistanceKm = duty.DistanceKm,
        CreatedAtUtc = duty.CreatedAtUtc,
        UpdatedAtUtc = duty.UpdatedAtUtc,
        Lines = duty.Lines
            .OrderBy(x => x.LineCode)
            .Select(x => new PlanningDutyLineDto
            {
                Id = x.Id,
                LineCode = x.LineCode,
                Variant = x.Variant,
                DistanceKm = x.DistanceKm
            })
            .ToList()
    };

    private static PlanningDutyDetailsDto ToDetailsDto(PlanningDuty duty) => new()
    {
        Id = duty.Id,
        DutyNumber = duty.DutyNumber,
        Name = duty.Name,
        ValidFrom = duty.ValidFrom,
        VehicleRequirement = duty.VehicleRequirement,
        StartTime = duty.StartTime,
        EndTime = duty.EndTime,
        TotalDurationMinutes = duty.TotalDurationMinutes,
        WorkMinutes = duty.WorkMinutes,
        BreakMinutes = duty.BreakMinutes,
        DrivingMinutes = duty.DrivingMinutes,
        DistanceKm = duty.DistanceKm,
        Notes = duty.Notes,
        SourceFileName = duty.SourceFileName,
        CreatedAtUtc = duty.CreatedAtUtc,
        UpdatedAtUtc = duty.UpdatedAtUtc,
        Lines = duty.Lines
            .OrderBy(x => x.LineCode)
            .Select(x => new PlanningDutyLineDto
            {
                Id = x.Id,
                LineCode = x.LineCode,
                Variant = x.Variant,
                DistanceKm = x.DistanceKm
            })
            .ToList(),
        Stops = duty.Stops
            .OrderBy(x => x.Sequence)
            .Select(x => new PlanningDutyStopDto
            {
                Id = x.Id,
                Sequence = x.Sequence,
                StopName = x.StopName,
                Km = x.Km,
                TripGroup = x.TripGroup,
                ArrivalTime = x.ArrivalTime,
                DepartureTime = x.DepartureTime,
                LineCode = x.LineCode
            })
            .ToList()
    };


    internal static void ValidateConfirmRequest(PlanningDutyPdfImportConfirmRequestDto request)
    {
        var errors = new List<string>();

        if (request.Duties.Count == 0)
        {
            errors.Add("Brak służb do importu.");
        }

        for (var index = 0; index < request.Duties.Count; index++)
        {
            var duty = request.Duties[index];
            var label = $"Służba #{index + 1}";

            if (string.IsNullOrWhiteSpace(duty.DutyNumber))
            {
                errors.Add($"{label}: numer służby jest wymagany.");
            }

            if (!duty.StartTime.HasValue)
            {
                errors.Add($"{label}: godzina rozpoczęcia jest wymagana.");
            }

            if (!duty.EndTime.HasValue)
            {
                errors.Add($"{label}: godzina zakończenia jest wymagana.");
            }

            OptionalText(duty.DutyNumber, DutyNumberMaxLength, $"{label}: numer służby jest za długi.", errors);
            OptionalText(duty.DutyName, NameMaxLength, $"{label}: nazwa jest za długa.", errors);
            OptionalText(duty.Line, 500, $"{label}: linia jest za długa.", errors);
            OptionalText(duty.VehicleRequirement, VehicleRequirementMaxLength, $"{label}: wymagany pojazd jest za długi.", errors);
            OptionalText(duty.Notes, NotesMaxLength, $"{label}: uwagi są za długie.", errors);
            NonNegative(duty.WorkingMinutes, $"{label}: czas pracy nie może być ujemny.", errors);
            NonNegative(duty.DrivingMinutes, $"{label}: czas jazdy nie może być ujemny.", errors);
            NonNegative(duty.BreakMinutes, $"{label}: czas przerwy nie może być ujemny.", errors);
            NonNegative(duty.DistanceKm, $"{label}: kilometry nie mogą być ujemne.", errors);

            foreach (var stop in duty.Stops)
            {
                OptionalText(stop.StopName, StopNameMaxLength, $"{label}: nazwa przystanku jest za długa.", errors);
                NonNegative(stop.Sequence, $"{label}: kolejność przystanku nie może być ujemna.", errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }

    private static PlanningDuty CreateDutyFromConfirmItem(
        PlanningDutyPdfImportConfirmItemDto item,
        string? sourceFileName,
        Guid companyId,
        DateTime now)
    {
        var duty = new PlanningDuty
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            CreatedAt = now,
            CreatedAtUtc = now
        };

        ApplyConfirmItem(duty, item, sourceFileName, now, setUpdatedAt: false);

        return duty;
    }

    private static void ApplyConfirmItem(
        PlanningDuty duty,
        PlanningDutyPdfImportConfirmItemDto item,
        string? sourceFileName,
        DateTime now,
        bool setUpdatedAt)
    {
        var dutyNumber = NormalizeRequired(item.DutyNumber);
        duty.DutyNumber = dutyNumber;
        duty.Name = NormalizeOptional(item.DutyName) ?? $"Służba {dutyNumber}";
        duty.ValidFrom = item.ValidFrom;
        duty.VehicleRequirement = NormalizeOptional(item.VehicleRequirement);
        duty.StartTime = item.StartTime;
        duty.EndTime = item.EndTime;
        duty.WorkMinutes = item.WorkingMinutes;
        duty.DrivingMinutes = item.DrivingMinutes;
        duty.BreakMinutes = item.BreakMinutes;
        duty.DistanceKm = item.DistanceKm;
        duty.Notes = NormalizeOptional(item.Notes);
        duty.SourceFileName = NormalizeOptional(sourceFileName);

        if (setUpdatedAt)
        {
            duty.UpdatedAtUtc = now;
        }

        duty.Lines.Clear();
        foreach (var line in SplitLineCodes(item.Line))
        {
            duty.Lines.Add(new PlanningDutyLine
            {
                Id = Guid.NewGuid(),
                PlanningDutyId = duty.Id,
                LineCode = line
            });
        }

        duty.Stops.Clear();
        foreach (var stop in item.Stops.OrderBy(x => x.Sequence))
        {
            var stopName = NormalizeOptional(stop.StopName);
            if (stopName is null)
            {
                continue;
            }

            duty.Stops.Add(new PlanningDutyStop
            {
                Id = Guid.NewGuid(),
                PlanningDutyId = duty.Id,
                Sequence = stop.Sequence,
                StopName = stopName,
                Km = stop.Km,
                ArrivalTime = stop.ArrivalTime,
                DepartureTime = stop.DepartureTime,
                LineCode = NormalizeOptional(stop.LineCode) ?? NormalizeSingleLineCode(item.Line)
            });
        }
    }

    private static bool IsConfirmItemIdentical(
        PlanningDuty duty,
        PlanningDutyPdfImportConfirmItemDto item,
        string? sourceFileName)
    {
        var dutyName = NormalizeOptional(item.DutyName) ?? $"Służba {NormalizeRequired(item.DutyNumber)}";

        return string.Equals(duty.Name, dutyName, StringComparison.Ordinal)
            && duty.ValidFrom == item.ValidFrom
            && string.Equals(duty.VehicleRequirement, NormalizeOptional(item.VehicleRequirement), StringComparison.Ordinal)
            && duty.StartTime == item.StartTime
            && duty.EndTime == item.EndTime
            && duty.WorkMinutes == item.WorkingMinutes
            && duty.DrivingMinutes == item.DrivingMinutes
            && duty.BreakMinutes == item.BreakMinutes
            && duty.DistanceKm == item.DistanceKm
            && string.Equals(duty.Notes, NormalizeOptional(item.Notes), StringComparison.Ordinal)
            && string.Equals(duty.SourceFileName, NormalizeOptional(sourceFileName), StringComparison.Ordinal)
            && string.Equals(GetLineKey(duty), GetLineKey(item.Line), StringComparison.OrdinalIgnoreCase)
            && AreStopsIdentical(duty.Stops, item.Stops, item.Line);
    }

    private static bool AreStopsIdentical(
        IEnumerable<PlanningDutyStop> existingStops,
        IEnumerable<PlanningDutyPdfImportConfirmStopDto> importedStops,
        string? line)
    {
        var existing = existingStops
            .OrderBy(x => x.Sequence)
            .Select(x => $"{x.Sequence}|{NormalizeOptional(x.StopName)}|{x.Km}|{x.ArrivalTime}|{x.DepartureTime}|{NormalizeOptional(x.LineCode)}")
            .ToList();
        var imported = importedStops
            .Where(x => !string.IsNullOrWhiteSpace(x.StopName))
            .OrderBy(x => x.Sequence)
            .Select(x => $"{x.Sequence}|{NormalizeOptional(x.StopName)}|{x.Km}|{x.ArrivalTime}|{x.DepartureTime}|{NormalizeOptional(x.LineCode) ?? NormalizeSingleLineCode(line)}")
            .ToList();

        return existing.SequenceEqual(imported, StringComparer.Ordinal);
    }

    private static PlanningDutyPdfImportConfirmResultItemDto CreateResultItem(
        PlanningDutyPdfImportConfirmItemDto item,
        string status,
        string message) => new()
    {
        DutyNumber = NormalizeOptional(item.DutyNumber),
        Line = NormalizeOptional(item.Line),
        Status = status,
        Message = message
    };

    private static string GetLineKey(PlanningDuty duty) =>
        string.Join(",", duty.Lines
            .Select(x => NormalizeOptional(x.LineCode))
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    private static string GetLineKey(string? line) =>
        string.Join(",", SplitLineCodes(line).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

    private static string? NormalizeSingleLineCode(string? line)
    {
        var lines = SplitLineCodes(line);
        return lines.Count == 1 ? lines[0] : null;
    }

    private static List<string> SplitLineCodes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value
            .Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void RequireText(
        string? value,
        string requiredMessage,
        int maxLength,
        string maxLengthMessage,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(requiredMessage);
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            errors.Add(maxLengthMessage);
        }
    }

    private static void OptionalText(
        string? value,
        int maxLength,
        string maxLengthMessage,
        ICollection<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim().Length > maxLength)
        {
            errors.Add(maxLengthMessage);
        }
    }

    private static void NonNegative(int? value, string message, ICollection<string> errors)
    {
        if (value < 0)
        {
            errors.Add(message);
        }
    }

    private static void NonNegative(decimal? value, string message, ICollection<string> errors)
    {
        if (value < 0)
        {
            errors.Add(message);
        }
    }
}







