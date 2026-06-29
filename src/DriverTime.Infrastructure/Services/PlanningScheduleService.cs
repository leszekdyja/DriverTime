using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningScheduleService : IPlanningScheduleService
{
    private const int NameMaxLength = 200;
    private const int NotesMaxLength = 4000;

    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public PlanningScheduleService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<List<PlanningScheduleListItemDto>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;

        return await _dbContext.PlanningSchedules
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ThenBy(x => x.Name)
            .Select(x => ToListDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningScheduleDto?> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleQuery(id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return schedule is null ? null : ToDto(schedule);
    }

    public async Task<PlanningScheduleDto> CreateScheduleAsync(
        PlanningScheduleCreateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateScheduleRequest(request);

        var now = DateTime.UtcNow;
        var schedule = new PlanningSchedule
        {
            Id = Guid.NewGuid(),
            CompanyId = _currentUser.CompanyId,
            Name = request.Name.Trim(),
            Year = request.Year,
            Month = request.Month,
            Notes = NormalizeOptional(request.Notes),
            CreatedAt = now,
            CreatedUtc = now
        };

        _dbContext.PlanningSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(schedule);
    }

    public async Task<PlanningScheduleDto?> UpdateScheduleAsync(
        Guid id,
        PlanningScheduleUpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateScheduleRequest(request);

        var schedule = await GetScheduleQuery(id).FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        schedule.Name = request.Name.Trim();
        schedule.Year = request.Year;
        schedule.Month = request.Month;
        schedule.Notes = NormalizeOptional(request.Notes);
        schedule.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(schedule);
    }

    public async Task<bool> DeleteScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await GetScheduleQuery(id).FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
        {
            return false;
        }

        _dbContext.PlanningSchedules.Remove(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<PlanningAssignmentDto?> UpsertAssignmentAsync(
        Guid scheduleId,
        PlanningAssignmentUpsertRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateAssignmentRequest(request);

        var companyId = _currentUser.CompanyId;
        var schedule = await _dbContext.PlanningSchedules
            .Where(x => x.Id == scheduleId && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (schedule is null)
        {
            return null;
        }

        var driver = await _dbContext.Drivers
            .Where(x => x.Id == request.DriverId && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (driver is null)
        {
            throw new PlanningDutyValidationException(new[] { "Nie można przypisać kierowcy spoza aktualnej firmy." });
        }

        PlanningDuty? duty = null;
        if (request.PlanningDutyId.HasValue)
        {
            duty = await _dbContext.PlanningDuties
                .Include(x => x.Lines)
                .Where(x => x.Id == request.PlanningDutyId.Value && x.CompanyId == companyId)
                .FirstOrDefaultAsync(cancellationToken);

            if (duty is null)
            {
                throw new PlanningDutyValidationException(new[] { "Nie można przypisać służby spoza aktualnej firmy." });
            }
        }

        var assignmentType = ParseAssignmentType(request.AssignmentType);
        var now = DateTime.UtcNow;
        var assignment = await _dbContext.PlanningAssignments
            .Include(x => x.Driver)
            .Include(x => x.PlanningDuty)
                .ThenInclude(x => x!.Lines)
            .Where(x =>
                x.CompanyId == companyId
                && x.PlanningScheduleId == scheduleId
                && x.DriverId == request.DriverId
                && x.Date == request.Date)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            assignment = new PlanningAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanningScheduleId = scheduleId,
                DriverId = request.DriverId,
                Date = request.Date,
                CreatedAt = now,
                CreatedUtc = now
            };
            _dbContext.PlanningAssignments.Add(assignment);
        }
        else
        {
            assignment.UpdatedUtc = now;
        }

        assignment.AssignmentType = assignmentType;
        assignment.PlanningDutyId = assignmentType == PlanningAssignmentType.Duty ? request.PlanningDutyId : null;
        assignment.Notes = NormalizeOptional(request.Notes);
        assignment.Driver = driver;
        assignment.PlanningDuty = assignment.PlanningDutyId.HasValue ? duty : null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToAssignmentDto(assignment);
    }

    public async Task<bool> DeleteAssignmentAsync(
        Guid scheduleId,
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var assignment = await _dbContext.PlanningAssignments
            .Where(x =>
                x.Id == assignmentId
                && x.PlanningScheduleId == scheduleId
                && x.CompanyId == companyId
                && x.PlanningSchedule.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return false;
        }

        _dbContext.PlanningAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }


    internal static PlanningSchedule CreateScheduleForCompany(
        PlanningScheduleCreateRequestDto request,
        Guid companyId,
        DateTime now)
    {
        ValidateScheduleRequest(request);

        return new PlanningSchedule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name.Trim(),
            Year = request.Year,
            Month = request.Month,
            Notes = NormalizeOptional(request.Notes),
            CreatedAt = now,
            CreatedUtc = now
        };
    }

    internal static bool IsAssignmentInCompanyScope(
        PlanningAssignment assignment,
        Guid scheduleId,
        Guid assignmentId,
        Guid companyId) =>
        assignment.Id == assignmentId
        && assignment.PlanningScheduleId == scheduleId
        && assignment.CompanyId == companyId
        && assignment.PlanningSchedule.CompanyId == companyId;
    internal static void ValidateScheduleRequest(PlanningScheduleCreateRequestDto request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add("Nazwa grafiku jest wymagana.");
        }
        else if (request.Name.Trim().Length > NameMaxLength)
        {
            errors.Add("Nazwa grafiku jest za długa.");
        }

        if (request.Year < 2000 || request.Year > 2100)
        {
            errors.Add("Rok grafiku musi być z zakresu 2000-2100.");
        }

        if (request.Month < 1 || request.Month > 12)
        {
            errors.Add("Miesiąc grafiku musi być z zakresu 1-12.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > NotesMaxLength)
        {
            errors.Add("Uwagi są za długie.");
        }

        ThrowIfErrors(errors);
    }

    internal static void ValidateAssignmentRequest(PlanningAssignmentUpsertRequestDto request)
    {
        var errors = new List<string>();
        var assignmentType = TryParseAssignmentType(request.AssignmentType);

        if (request.DriverId == Guid.Empty)
        {
            errors.Add("Wybierz kierowcę dla przypisania.");
        }

        if (assignmentType is null)
        {
            errors.Add("Nieznany typ przypisania grafiku.");
        }
        else if (assignmentType == PlanningAssignmentType.Duty && !request.PlanningDutyId.HasValue)
        {
            errors.Add("Dla typu Służba wybierz służbę z biblioteki.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > 2000)
        {
            errors.Add("Notatka przypisania jest za długa.");
        }

        ThrowIfErrors(errors);
    }

    internal static PlanningAssignment UpsertAssignmentForCompany(
        IList<PlanningAssignment> assignments,
        PlanningAssignmentUpsertRequestDto request,
        PlanningSchedule schedule,
        Driver driver,
        PlanningDuty? duty,
        Guid companyId,
        DateTime now)
    {
        ValidateAssignmentRequest(request);

        if (schedule.CompanyId != companyId)
        {
            throw new PlanningDutyValidationException(new[] { "Grafik nie należy do aktualnej firmy." });
        }

        if (driver.CompanyId != companyId)
        {
            throw new PlanningDutyValidationException(new[] { "Nie można przypisać kierowcy spoza aktualnej firmy." });
        }

        var assignmentType = ParseAssignmentType(request.AssignmentType);
        if (assignmentType == PlanningAssignmentType.Duty)
        {
            if (duty is null || duty.CompanyId != companyId)
            {
                throw new PlanningDutyValidationException(new[] { "Nie można przypisać służby spoza aktualnej firmy." });
            }
        }

        var assignment = assignments.FirstOrDefault(x =>
            x.CompanyId == companyId
            && x.PlanningScheduleId == schedule.Id
            && x.DriverId == request.DriverId
            && x.Date == request.Date);

        if (assignment is null)
        {
            assignment = new PlanningAssignment
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanningScheduleId = schedule.Id,
                DriverId = request.DriverId,
                Date = request.Date,
                CreatedAt = now,
                CreatedUtc = now
            };
            assignments.Add(assignment);
        }
        else
        {
            assignment.UpdatedUtc = now;
        }

        assignment.AssignmentType = assignmentType;
        assignment.PlanningDutyId = assignmentType == PlanningAssignmentType.Duty ? request.PlanningDutyId : null;
        assignment.Driver = driver;
        assignment.PlanningDuty = assignmentType == PlanningAssignmentType.Duty ? duty : null;
        assignment.Notes = NormalizeOptional(request.Notes);

        return assignment;
    }

    private IQueryable<PlanningSchedule> GetScheduleQuery(Guid id)
    {
        var companyId = _currentUser.CompanyId;

        return _dbContext.PlanningSchedules
            .Include(x => x.Assignments)
                .ThenInclude(x => x.Driver)
            .Include(x => x.Assignments)
                .ThenInclude(x => x.PlanningDuty)
                    .ThenInclude(x => x!.Lines)
            .Where(x => x.Id == id && x.CompanyId == companyId);
    }

    private static PlanningScheduleListItemDto ToListDto(PlanningSchedule schedule) => new()
    {
        Id = schedule.Id,
        Name = schedule.Name,
        Year = schedule.Year,
        Month = schedule.Month,
        Notes = schedule.Notes,
        CreatedUtc = schedule.CreatedUtc,
        UpdatedUtc = schedule.UpdatedUtc,
        AssignmentsCount = schedule.Assignments.Count
    };

    private static PlanningScheduleDto ToDto(PlanningSchedule schedule) => new()
    {
        Id = schedule.Id,
        Name = schedule.Name,
        Year = schedule.Year,
        Month = schedule.Month,
        Notes = schedule.Notes,
        CreatedUtc = schedule.CreatedUtc,
        UpdatedUtc = schedule.UpdatedUtc,
        AssignmentsCount = schedule.Assignments.Count,
        Assignments = schedule.Assignments
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Driver.LastName)
            .ThenBy(x => x.Driver.FirstName)
            .Select(ToAssignmentDto)
            .ToList()
    };

    private static PlanningAssignmentDto ToAssignmentDto(PlanningAssignment assignment) => new()
    {
        Id = assignment.Id,
        Date = assignment.Date,
        DriverId = assignment.DriverId,
        DriverFullName = FormatDriverName(assignment.Driver),
        PlanningDutyId = assignment.PlanningDutyId,
        DutyNumber = assignment.PlanningDuty?.DutyNumber,
        Line = assignment.PlanningDuty is null ? null : GetLineKey(assignment.PlanningDuty),
        StartTime = assignment.PlanningDuty?.StartTime,
        EndTime = assignment.PlanningDuty?.EndTime,
        AssignmentType = assignment.AssignmentType.ToString(),
        Notes = assignment.Notes
    };

    private static string FormatDriverName(Driver driver)
    {
        var name = $"{driver.FirstName} {driver.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? driver.CardNumber : name;
    }

    private static string GetLineKey(PlanningDuty duty) =>
        string.Join(", ", duty.Lines
            .Select(x => x.LineCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x));

    private static PlanningAssignmentType ParseAssignmentType(string? value) =>
        TryParseAssignmentType(value) ?? throw new PlanningDutyValidationException(new[] { "Nieznany typ przypisania grafiku." });

    private static PlanningAssignmentType? TryParseAssignmentType(string? value) =>
        Enum.TryParse<PlanningAssignmentType>(value, ignoreCase: true, out var assignmentType)
            ? assignmentType
            : null;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }
}

