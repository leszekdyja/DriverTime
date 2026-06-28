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
        UpdatedAtUtc = duty.UpdatedAtUtc
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
