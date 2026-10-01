using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public class PlanningManualAssignmentService : IPlanningManualAssignmentService
{
    private readonly DriverTimeDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlanningAssignmentValidationService _validationService;

    public PlanningManualAssignmentService(
        DriverTimeDbContext dbContext,
        ICurrentUserService currentUser,
        IPlanningAssignmentValidationService validationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _validationService = validationService;
    }

    public async Task<PlanningAssignmentDto> CreateManualAsync(
        PlanningManualAssignmentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequestShape(request);
        var companyId = _currentUser.CompanyId;
        var driver = await LoadDriverAsync(request.DriverId, companyId, cancellationToken);
        var assignmentType = ResolveAssignmentType(request);
        var duty = assignmentType == PlanningAssignmentType.Duty
            ? await ResolveDutyAsync(request, companyId, cancellationToken)
            : null;
        var schedule = await EnsureScheduleAsync(companyId, request.Date, cancellationToken);
        var context = await LoadValidationContextAsync(companyId, driver.Id, request.Date, cancellationToken);
        IReadOnlyCollection<PlanningAssignmentRule> rules = duty is null ? Array.Empty<PlanningAssignmentRule>() : await LoadAssignmentRulesAsync(companyId, cancellationToken);

        ValidateManualAssignment(companyId, null, driver, duty, request.Date, context, rules);

        var now = DateTime.UtcNow;
        var assignment = new PlanningAssignment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanningScheduleId = schedule.Id,
            PlanningSchedule = schedule,
            DriverId = driver.Id,
            Driver = driver,
            PlanningDutyId = duty?.Id,
            PlanningDuty = duty,
            Date = request.Date,
            Status = PlanningAssignmentStatus.Manual,
            AssignmentType = assignmentType,
            Notes = BuildNotes(request, assignmentType),
            CreatedAt = now,
            CreatedUtc = now
        };
        ApplyAssignmentTimes(assignment, duty);

        _dbContext.PlanningAssignments.Add(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(assignment);
    }

    public async Task<PlanningAssignmentDto?> UpdateAsync(
        Guid id,
        PlanningManualAssignmentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequestShape(request);
        var companyId = _currentUser.CompanyId;
        var assignment = await _dbContext.PlanningAssignments
            .Include(x => x.Driver)
            .Include(x => x.PlanningDuty)
                .ThenInclude(x => x!.Lines)
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment is null)
        {
            return null;
        }

        var driver = await LoadDriverAsync(request.DriverId, companyId, cancellationToken);
        var assignmentType = ResolveAssignmentType(request);
        var duty = assignmentType == PlanningAssignmentType.Duty
            ? await ResolveDutyAsync(request, companyId, cancellationToken)
            : null;
        var schedule = await EnsureScheduleAsync(companyId, request.Date, cancellationToken);
        var context = await LoadValidationContextAsync(companyId, driver.Id, request.Date, cancellationToken);
        IReadOnlyCollection<PlanningAssignmentRule> rules = duty is null ? Array.Empty<PlanningAssignmentRule>() : await LoadAssignmentRulesAsync(companyId, cancellationToken);

        ValidateManualAssignment(companyId, assignment.Id, driver, duty, request.Date, context, rules);

        assignment.PlanningScheduleId = schedule.Id;
        assignment.PlanningSchedule = schedule;
        assignment.DriverId = driver.Id;
        assignment.Driver = driver;
        assignment.PlanningDutyId = duty?.Id;
        assignment.PlanningDuty = duty;
        assignment.Date = request.Date;
        assignment.Status = PlanningAssignmentStatus.Manual;
        assignment.AssignmentType = assignmentType;
        assignment.Notes = BuildNotes(request, assignmentType);
        assignment.UpdatedUtc = DateTime.UtcNow;
        ApplyAssignmentTimes(assignment, duty);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(assignment);
    }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var companyId = _currentUser.CompanyId;
        var assignment = await _dbContext.PlanningAssignments
            .Where(x => x.Id == id && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (assignment is null)
        {
            return false;
        }

        _dbContext.PlanningAssignments.Remove(assignment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Driver> LoadDriverAsync(Guid driverId, Guid companyId, CancellationToken cancellationToken)
    {
        var driver = await _dbContext.Drivers
            .Where(x => x.Id == driverId && x.CompanyId == companyId)
            .FirstOrDefaultAsync(cancellationToken);
        return driver ?? throw new PlanningDutyValidationException(new[] { "Kierowca nie należy do bieżącej firmy." });
    }

    private async Task<PlanningDuty> ResolveDutyAsync(
        PlanningManualAssignmentRequestDto request,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        IQueryable<PlanningDuty> query = _dbContext.PlanningDuties
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId);

        if (request.DutyId.HasValue)
        {
            query = query.Where(x => x.Id == request.DutyId.Value);
        }
        else
        {
            var code = PlanningAssignmentConstraintEvaluator.NormalizeDutyNumber(request.EntryCode);
            query = query.Where(x => x.DutyNumber.ToUpper() == code);
        }

        var duty = await query
            .OrderByDescending(x => x.ValidFrom)
            .ThenBy(x => x.DutyNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return duty ?? throw new PlanningDutyValidationException(new[] { "Służba nie należy do bieżącej firmy." });
    }

    private async Task<PlanningSchedule> EnsureScheduleAsync(
        Guid companyId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var schedule = await _dbContext.PlanningSchedules
            .Where(x => x.CompanyId == companyId && x.Year == date.Year && x.Month == date.Month)
            .OrderBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (schedule is not null)
        {
            return schedule;
        }

        var now = DateTime.UtcNow;
        schedule = new PlanningSchedule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = $"Plan ręczny {date.Year}-{date.Month:00}",
            Year = date.Year,
            Month = date.Month,
            CreatedAt = now,
            CreatedUtc = now
        };
        _dbContext.PlanningSchedules.Add(schedule);
        return schedule;
    }

    private async Task<ValidationContext> LoadValidationContextAsync(
        Guid companyId,
        Guid driverId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var dateFrom = date.AddDays(-8);
        var dateTo = date.AddDays(8);
        var assignments = await _dbContext.PlanningAssignments
            .Include(x => x.PlanningDuty)
            .Where(x => x.CompanyId == companyId && x.DriverId == driverId && x.Date >= dateFrom && x.Date <= dateTo)
            .ToListAsync(cancellationToken);
        var availabilities = await _dbContext.PlanningDriverAvailabilities
            .Where(x => x.CompanyId == companyId && x.DriverId == driverId && x.DateFrom <= date && x.DateTo >= date)
            .ToListAsync(cancellationToken);
        return new ValidationContext(assignments, availabilities);
    }

    internal async Task<List<PlanningAssignmentRule>> LoadAssignmentRulesAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return await _dbContext.PlanningDriverDutyRules
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new PlanningAssignmentRule
            {
                CompanyId = x.CompanyId,
                DriverId = x.DriverId,
                DutyId = x.PlanningDutyId,
                Type = x.Type == PlanningDriverDutyRuleType.Preferred
                    ? PlanningAssignmentRuleType.Preferred
                    : PlanningAssignmentRuleType.Forbidden,
                DateFrom = x.ValidFrom,
                DateTo = x.ValidTo,
                Note = x.Notes
            })
            .ToListAsync(cancellationToken);
    }

    private static void ValidateRequestShape(PlanningManualAssignmentRequestDto request)
    {
        var errors = new List<string>();
        if (request.DriverId == Guid.Empty)
        {
            errors.Add("Wybierz kierowcę.");
        }

        if (!request.DutyId.HasValue && string.IsNullOrWhiteSpace(request.EntryCode))
        {
            errors.Add("Wybierz służbę albo typ wpisu.");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes) && request.Notes.Trim().Length > 2000)
        {
            errors.Add("Notatka przypisania jest za długa.");
        }

        if (errors.Count > 0)
        {
            throw new PlanningDutyValidationException(errors);
        }
    }

    private void ValidateManualAssignment(
        Guid companyId,
        Guid? assignmentId,
        Driver driver,
        PlanningDuty? duty,
        DateOnly date,
        ValidationContext context,
        IReadOnlyCollection<PlanningAssignmentRule> rules)
    {
        if (duty is not null)
        {
            _validationService.ValidateManualAssignment(new PlanningAssignmentValidationRequest
            {
                CompanyId = companyId,
                AssignmentId = assignmentId,
                Driver = driver,
                PlanningDuty = duty,
                Date = date,
                ExistingAssignments = context.Assignments,
                Availabilities = context.Availabilities,
                AssignmentRules = rules
            });
            return;
        }

        var otherSameDay = context.Assignments.Any(x => x.Id != assignmentId && x.DriverId == driver.Id && x.Date == date);
        if (otherSameDay)
        {
            throw new PlanningDutyValidationException(new[] { "Kierowca ma już wpis w wybranym dniu." });
        }
    }

    private static PlanningAssignmentType ResolveAssignmentType(PlanningManualAssignmentRequestDto request)
    {
        var code = request.EntryCode?.Trim();
        if (string.Equals(code, "Vacation", StringComparison.OrdinalIgnoreCase)) return PlanningAssignmentType.Vacation;
        if (string.Equals(code, "SickLeave", StringComparison.OrdinalIgnoreCase)) return PlanningAssignmentType.SickLeave;
        if (string.Equals(code, "Unavailable", StringComparison.OrdinalIgnoreCase)) return PlanningAssignmentType.Other;
        return PlanningAssignmentType.Duty;
    }

    private static string? BuildNotes(PlanningManualAssignmentRequestDto request, PlanningAssignmentType assignmentType)
    {
        var note = NormalizeOptional(request.Notes);
        if (assignmentType == PlanningAssignmentType.Other && string.Equals(request.EntryCode, "Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(note) ? "Niedostępność" : $"Niedostępność. {note}";
        }

        return note;
    }

    private static void ApplyAssignmentTimes(PlanningAssignment assignment, PlanningDuty? duty)
    {
        if (duty is null)
        {
            assignment.StartDateTime = null;
            assignment.EndDateTime = null;
            return;
        }

        var classification = PlanningEntryClassifier.Classify(duty);
        if (!classification.IsTimedWork)
        {
            assignment.StartDateTime = null;
            assignment.EndDateTime = null;
            return;
        }

        var interval = PlanningAutoGeneratorService.BuildInterval(assignment.Date, duty);
        assignment.StartDateTime = interval?.Start;
        assignment.EndDateTime = interval?.End;
    }

    private static PlanningAssignmentDto ToDto(PlanningAssignment assignment) => new()
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
        StartDateTime = assignment.StartDateTime,
        EndDateTime = assignment.EndDateTime,
        Status = assignment.Status.ToString(),
        AssignmentType = assignment.AssignmentType.ToString(),
        Notes = assignment.Notes
    };

    private static string FormatDriverName(Driver driver)
    {
        var name = $"{driver.LastName} {driver.FirstName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? driver.CardNumber : name;
    }

    private static string GetLineKey(PlanningDuty duty) =>
        string.Join(", ", duty.Lines.Select(x => x.LineCode).Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x));

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ValidationContext(
        IReadOnlyCollection<PlanningAssignment> Assignments,
        IReadOnlyCollection<PlanningDriverAvailability> Availabilities);
}


