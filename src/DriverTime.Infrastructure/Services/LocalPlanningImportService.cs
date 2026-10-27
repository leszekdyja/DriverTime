using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public sealed class LocalPlanningImportService(DriverTimeDbContext db, ICurrentUserService user) : ILocalPlanningImportService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<LocalPlanningImportOptionsDto> GetOptionsAsync(Guid? operatingCompanyId, CancellationToken ct)
    {
        var scope = await ResolveScope(operatingCompanyId, ct);
        var drivers = await VisibleDrivers(scope).OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync(ct);
        var duties = await VisibleDuties(scope).OrderBy(x => x.DutyNumber).ThenByDescending(x => x.ValidFrom).ToListAsync(ct);
        return new()
        {
            OperatingCompanyId = scope, CanChooseCompany = !user.OperatingCompanyId.HasValue,
            Companies = await db.OperatingCompanies.AsNoTracking().Where(x => x.CompanyId == user.CompanyId
                && (!user.OperatingCompanyId.HasValue || x.Id == user.OperatingCompanyId))
                .Select(x => new LocalPlanningOptionDto(x.Id, x.Name)).ToListAsync(ct),
            Drivers = drivers.Select(x => new LocalPlanningOptionDto(x.Id, (x.FirstName + " " + x.LastName).Trim())).ToList(),
            Duties = duties.Select(x => new LocalPlanningOptionDto(x.Id,
                $"{x.DutyNumber} — {x.Name} ({x.ValidFrom?.ToString("yyyy-MM-dd") ?? "bez daty"}; {x.StartTime:HH:mm}–{x.EndTime:HH:mm})")).ToList(),
            Schedules = await VisibleSchedules(scope).OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .Select(x => new LocalPlanningScheduleOptionDto(x.Id, x.Name, x.Year, x.Month)).ToListAsync(ct)
        };
    }

    public async Task<LocalPlanningImportPlanDto> PrepareAsync(LocalPlanningImportRequestDto request, CancellationToken ct)
        => (await BuildPlan(request, ct)).Plan;

    public async Task<LocalPlanningImportResultDto> ApplyAsync(LocalPlanningImportRequestDto request, CancellationToken ct)
    {
        EnsureUser();
        ValidateRequestShape(request);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var hash = RequestHash(request);
        var existing = await db.LocalPlanningSyncBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OperationId, ct);
        if (existing is not null)
        {
            if (existing.CompanyId != user.CompanyId || existing.OperatingCompanyId != request.OperatingCompanyId
                || (user.OperatingCompanyId.HasValue && existing.OperatingCompanyId != user.OperatingCompanyId))
                throw new UnauthorizedAccessException();
            if (existing.RequestHash != hash || existing.RevertedAtUtc.HasValue)
                throw new InvalidOperationException("Ta operacja została już użyta lub cofnięta. Przygotuj nowy podgląd.");
            return JsonSerializer.Deserialize<LocalPlanningImportResultDto>(existing.ResultJson, Json)!;
        }
        var prepared = await BuildPlan(request, ct);
        if (!prepared.Plan.CanApply || request.RevisionToken != prepared.Plan.RevisionToken)
            throw new InvalidOperationException("Podgląd jest nieaktualny lub wymaga rozstrzygnięcia kolizji i potwierdzeń. Sprawdź ponownie.");
        var utcNow = DateTime.UtcNow;
        var now = new DateTime(utcNow.Ticks - utcNow.Ticks % 10, DateTimeKind.Utc);
        var schedule = prepared.Schedule;
        if (schedule is null)
        {
            schedule = new PlanningSchedule { Id = Guid.NewGuid(), CompanyId = user.CompanyId,
                OperatingCompanyId = request.OperatingCompanyId, Year = request.Year, Month = request.Month,
                Name = $"Import z komputera {request.Year}-{request.Month:00}", CreatedAt = now, CreatedUtc = now };
            db.PlanningSchedules.Add(schedule);
        }
        var changes = new List<Change>();
        var created = 0; var updated = 0; var kept = 0;
        foreach (var cell in prepared.Cells)
        {
            if (cell.Existing is not null && SameContent(cell.Existing, cell.Desired)) continue;
            if (cell.Existing is not null && request.Decisions[cell.SourceId] == "KeepServer") { kept++; continue; }
            var before = cell.Existing is null ? null : Capture(cell.Existing);
            var assignment = cell.Existing ?? new PlanningAssignment { Id = Guid.NewGuid(), CompanyId = user.CompanyId,
                PlanningScheduleId = schedule.Id, DriverId = cell.Desired.DriverId, Date = cell.Desired.Date,
                CreatedAt = now, CreatedUtc = now };
            if (cell.Existing is null) { db.PlanningAssignments.Add(assignment); created++; }
            else { assignment.UpdatedUtc = now; updated++; }
            assignment.PlanningDutyId = cell.Desired.PlanningDutyId;
            assignment.AssignmentType = cell.Desired.AssignmentType; assignment.Status = cell.Desired.Status;
            assignment.StartDateTime = cell.Desired.StartDateTime; assignment.EndDateTime = cell.Desired.EndDateTime;
            assignment.Notes = cell.Desired.Notes;
            changes.Add(new(assignment.Id, before, Capture(assignment)));
        }
        if (created + updated > 0) schedule.UpdatedUtc = now;
        var result = new LocalPlanningImportResultDto(request.OperationId, schedule.Id, created, updated, kept,
            prepared.Plan.UnchangedEntries, prepared.Plan.SkippedEntries);
        db.LocalPlanningSyncBatches.Add(new LocalPlanningSyncBatch { Id = request.OperationId, CompanyId = user.CompanyId,
            OperatingCompanyId = request.OperatingCompanyId, SourceId = request.Snapshot.SourceId, UserId = user.UserId,
            PlanningScheduleId = schedule.Id, Year = request.Year, Month = request.Month, CreatedAtUtc = now,
            RequestHash = hash, RequestJson = JsonSerializer.Serialize(request, Json),
            BackupJson = JsonSerializer.Serialize(changes, Json), ResultJson = JsonSerializer.Serialize(result, Json) });
        // The before-images and the assignments are committed atomically.
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<List<LocalPlanningBatchListItemDto>> GetBatchesAsync(CancellationToken ct)
    {
        EnsureUser();
        return await db.LocalPlanningSyncBatches.AsNoTracking().Where(x => x.CompanyId == user.CompanyId
            && (!user.OperatingCompanyId.HasValue || x.OperatingCompanyId == user.OperatingCompanyId))
            .OrderByDescending(x => x.CreatedAtUtc).Take(30)
            .Select(x => new LocalPlanningBatchListItemDto(x.Id, x.PlanningScheduleId, x.Year, x.Month, x.CreatedAtUtc, x.RevertedAtUtc)).ToListAsync(ct);
    }

    public async Task RevertAsync(Guid batchId, CancellationToken ct)
    {
        EnsureUser();
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var batch = await db.LocalPlanningSyncBatches.SingleOrDefaultAsync(x => x.Id == batchId
            && x.CompanyId == user.CompanyId && (!user.OperatingCompanyId.HasValue || x.OperatingCompanyId == user.OperatingCompanyId), ct)
            ?? throw new ArgumentException("Nie znaleziono importu w bieżącej firmie.");
        if (batch.RevertedAtUtc.HasValue) return;
        var changes = JsonSerializer.Deserialize<List<Change>>(batch.BackupJson, Json)!;
        var ids = changes.Select(x => x.Id).ToList();
        var assignments = await db.PlanningAssignments.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var scope = await ResolveScope(batch.OperatingCompanyId, ct);
        var allowedDrivers = await VisibleDrivers(scope).Select(x => x.Id).ToListAsync(ct);
        var previousDutyIds = changes.Where(x => x.Before?.PlanningDutyId is not null).Select(x => x.Before!.PlanningDutyId!.Value).Distinct().ToArray();
        var previousDutiesVisible = await VisibleDuties(scope).CountAsync(x => previousDutyIds.Contains(x.Id), ct);
        if (previousDutiesVisible != previousDutyIds.Length)
            throw new InvalidOperationException("Poprzednia służba nie należy już do bieżącej firmy.");
        var schedule = await VisibleSchedules(scope).SingleOrDefaultAsync(x => x.Id == batch.PlanningScheduleId, ct)
            ?? throw new InvalidOperationException("Docelowy grafik zmienił firmę lub został usunięty.");
        foreach (var change in changes)
        {
            if (!assignments.TryGetValue(change.Id, out var current) || current.CompanyId != user.CompanyId
                || !allowedDrivers.Contains(current.DriverId) || current.PlanningScheduleId != schedule.Id
                || JsonSerializer.Serialize(Capture(current), Json) != JsonSerializer.Serialize(change.After, Json))
                throw new InvalidOperationException("Wpisy były później zmieniane. Cofnięcie nie może ich nadpisać.");
        }
        foreach (var change in changes)
        {
            var current = assignments[change.Id];
            if (change.Before is null) db.PlanningAssignments.Remove(current);
            else Restore(current, change.Before);
        }
        batch.RevertedAtUtc = DateTime.UtcNow;
        schedule.UpdatedUtc = batch.RevertedAtUtc;
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
    }

    private async Task<Prepared> BuildPlan(LocalPlanningImportRequestDto request, CancellationToken ct)
    {
        var scope = await ResolveScope(request.OperatingCompanyId, ct);
        if (request.OperatingCompanyId != scope) throw new UnauthorizedAccessException();
        ValidateRequestShape(request);
        var local = request.Snapshot.Tables;
        var selected = local.Entries.Where(x => x.Date.Year == request.Year && x.Date.Month == request.Month).OrderBy(x => x.Id).ToList();
        if (selected.Count == 0) throw new ArgumentException("Eksport nie zawiera wpisów wybranego miesiąca.");
        if (request.DriverMappings.Values.Distinct().Count() != request.DriverMappings.Count)
            throw new ArgumentException("Nie można przypisać dwóch lokalnych kierowców do tej samej osoby.");
        var localDriverIds = local.Drivers.Select(x => x.Id).ToHashSet();
        var localDutyIds = local.Duties.Select(x => x.Id).ToHashSet();
        var selectedIds = selected.Select(x => x.Id).ToHashSet();
        if (request.DriverMappings.Keys.Any(id => !localDriverIds.Contains(id))
            || request.DutyMappings.Keys.Any(id => !localDutyIds.Contains(id))
            || request.Decisions.Any(x => !selectedIds.Contains(x.Key) || (x.Value != "KeepServer" && x.Value != "UseLocal")))
            throw new ArgumentException("Nieprawidłowe dopasowania lub decyzje o kolizjach.");
        var driverIds = request.DriverMappings.Values.ToArray();
        var dutyIds = request.DutyMappings.Values.ToArray();
        var drivers = await VisibleDrivers(scope).Where(x => driverIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var duties = await VisibleDuties(scope).Where(x => dutyIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (drivers.Count != request.DriverMappings.Count || duties.Count != request.DutyMappings.Values.Distinct().Count())
            throw new ArgumentException("Wybrano kierowcę lub służbę spoza docelowej firmy.");
        PlanningSchedule? schedule;
        if (request.ScheduleId.HasValue)
            schedule = await VisibleSchedules(scope).SingleOrDefaultAsync(x => x.Id == request.ScheduleId, ct)
                ?? throw new ArgumentException("Grafik nie należy do docelowej firmy.");
        else
        {
            var previous = await db.LocalPlanningSyncBatches.AsNoTracking().Where(x => x.CompanyId == user.CompanyId
                && x.OperatingCompanyId == scope && x.SourceId == request.Snapshot.SourceId && x.Year == request.Year && x.Month == request.Month)
                .OrderByDescending(x => x.CreatedAtUtc).Select(x => (Guid?)x.PlanningScheduleId).FirstOrDefaultAsync(ct);
            schedule = previous.HasValue ? await VisibleSchedules(scope).SingleOrDefaultAsync(x => x.Id == previous, ct) : null;
            if (previous.HasValue && schedule is null) throw new InvalidOperationException("Poprzedni grafik importu zmienił firmę. Wybierz docelowy grafik ponownie.");
        }
        if (schedule is not null && (schedule.Year != request.Year || schedule.Month != request.Month))
            throw new ArgumentException("Miesiąc grafiku docelowego nie pasuje do importu.");
        var existing = schedule is null ? new List<PlanningAssignment>() : await db.PlanningAssignments
            .Where(x => x.PlanningScheduleId == schedule.Id).OrderBy(x => x.Id).ToListAsync(ct);
        if (existing.Any(x => x.CompanyId != user.CompanyId)) throw new InvalidOperationException("Niespójne przypisania firmy grafiku.");
        var existingByCell = existing.ToDictionary(x => (x.DriverId, x.Date));
        var existingDutyIds = existing.Where(x => x.PlanningDutyId.HasValue).Select(x => x.PlanningDutyId!.Value).Distinct().ToList();
        var existingDutyLabels = await VisibleDuties(scope).Where(x => existingDutyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DutyNumber, ct);
        var plan = new LocalPlanningImportPlanDto { SelectedEntries = selected.Count, ScheduleId = schedule?.Id };
        var cells = new List<Cell>();
        foreach (var entry in selected)
        {
            if (!request.DriverMappings.TryGetValue(entry.DriverId, out var driverId)
                || !request.DutyMappings.TryGetValue(entry.DutyId, out var dutyId)) { plan.SkippedEntries++; continue; }
            var duty = duties[dutyId];
            var note = entry.Details.TryGetValue("note", out var noteElement) && noteElement.ValueKind == JsonValueKind.String ? noteElement.GetString() : null;
            if (note?.Length > 2000) throw new ArgumentException("Uwagi wpisu przekraczają 2000 znaków. Skróć je w eksporcie.");
            var desired = new PlanningAssignment { DriverId = driverId, Date = entry.Date, PlanningDutyId = dutyId,
                AssignmentType = PlanningAssignmentType.Duty, Notes = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Status = entry.EntrySource == "manual" ? PlanningAssignmentStatus.Manual : PlanningAssignmentStatus.Generated };
            if (PlanningEntryClassifier.Classify(duty).IsTimedWork)
            {
                var interval = PlanningAutoGeneratorService.BuildInterval(entry.Date, duty);
                desired.StartDateTime = interval?.Start; desired.EndDateTime = interval?.End;
            }
            existingByCell.TryGetValue((driverId, entry.Date), out var current);
            cells.Add(new(entry.Id, current, desired));
            if (current is null) plan.NewEntries++;
            else if (SameContent(current, desired)) plan.UnchangedEntries++;
            else
            {
                if (current.PlanningDutyId.HasValue && !existingDutyLabels.ContainsKey(current.PlanningDutyId.Value))
                    throw new InvalidOperationException("Kolizyjna służba nie należy do docelowej firmy.");
                var serverDuty = current.PlanningDutyId.HasValue ? existingDutyLabels[current.PlanningDutyId.Value] : current.AssignmentType.ToString();
                plan.Conflicts.Add(new(entry.Id, entry.Date, (drivers[driverId].FirstName + " " + drivers[driverId].LastName).Trim(),
                    duty.DutyNumber, serverDuty ?? "Brak służby", current.Status.ToString(), request.Decisions.GetValueOrDefault(entry.Id)));
            }
        }
        plan.Warnings.Add("Zapis jest wyłącznie ręczny i obejmuje tylko wybrany miesiąc. Nie usuwa pozostałych wpisów ani nie zmienia bazy na komputerze.");
        plan.Warnings.Add("Import komórek używa czasów i RBH wybranych definicji służb na VPS. Dodatkowe ustawienia lokalnego generatora są archiwizowane, ale nie są jeszcze stosowane. To nie jest pełna synchronizacja ustawień.");
        if (scope is null) plan.Warnings.Add("Grafik będzie widoczny tylko na koncie głównym; nie przypisano go do osobnej firmy.");
        if (plan.SkippedEntries > 0) plan.Warnings.Add($"Pominięte wpisy bez dopasowania: {plan.SkippedEntries}. Wymagana osobna zgoda na import częściowy.");
        plan.CanApply = cells.Count > 0 && (plan.SkippedEntries == 0 || request.AllowSkipping)
            && request.AcknowledgeDefinitions && plan.Conflicts.All(x => x.Decision is "KeepServer" or "UseLocal");
        plan.RevisionToken = Hash(new { Request = RequestHash(request), Schedule = schedule is null ? null : new { schedule.Id, schedule.UpdatedUtc, schedule.Name },
            Existing = existing.Select(Capture), Drivers = drivers.Values.OrderBy(x => x.Id).Select(x => new { x.Id, x.OperatingCompanyId, x.FirstName, x.LastName }),
            Duties = duties.Values.OrderBy(x => x.Id).Select(x => new { x.Id, x.OperatingCompanyId, x.DutyNumber, x.StartTime, x.EndTime, x.WorkMinutes, x.TotalDurationMinutes }) });
        return new(plan, schedule, cells);
    }

    private async Task<Guid?> ResolveScope(Guid? requested, CancellationToken ct)
    {
        EnsureUser();
        if (user.OperatingCompanyId.HasValue && requested.HasValue && requested != user.OperatingCompanyId) throw new UnauthorizedAccessException();
        var scope = user.OperatingCompanyId ?? requested;
        if (scope.HasValue && !await db.OperatingCompanies.AnyAsync(x => x.Id == scope && x.CompanyId == user.CompanyId, ct))
            throw new UnauthorizedAccessException();
        return scope;
    }
    private void EnsureUser()
    {
        if (!user.IsAuthenticated || user.CompanyId == Guid.Empty || user.IsMobileDriver) throw new UnauthorizedAccessException();
    }
    private static void ValidateRequestShape(LocalPlanningImportRequestDto request)
    {
        if (request.Snapshot is null || request.OperationId == Guid.Empty || request.Year is < 2000 or > 2100 || request.Month is < 1 or > 12
            || request.DriverMappings is null || request.DutyMappings is null || request.Decisions is null)
            throw new ArgumentException("Wybierz miesiąc i przygotuj dopasowania importu.");
        LocalPlanningSyncPreviewService.Validate(request.Snapshot);
    }
    private IQueryable<Driver> VisibleDrivers(Guid? scope) => db.Drivers.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && (!scope.HasValue || x.OperatingCompanyId == scope));
    private IQueryable<PlanningDuty> VisibleDuties(Guid? scope) => db.PlanningDuties.AsNoTracking().Where(x => x.CompanyId == user.CompanyId && (!scope.HasValue || x.OperatingCompanyId == scope));
    private IQueryable<PlanningSchedule> VisibleSchedules(Guid? scope) => db.PlanningSchedules.Where(x => x.CompanyId == user.CompanyId && x.OperatingCompanyId == scope);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static string RequestHash(LocalPlanningImportRequestDto request) => Hash(new { request.Snapshot, request.OperationId, request.Year, request.Month,
        request.ScheduleId, request.OperatingCompanyId, Drivers = request.DriverMappings.OrderBy(x => x.Key), Duties = request.DutyMappings.OrderBy(x => x.Key),
        Decisions = request.Decisions.OrderBy(x => x.Key), request.AllowSkipping, request.AcknowledgeDefinitions });
    private static bool SameContent(PlanningAssignment a, PlanningAssignment b) => a.DriverId == b.DriverId && a.Date == b.Date
        && a.PlanningDutyId == b.PlanningDutyId && a.AssignmentType == b.AssignmentType && a.Status == b.Status
        && a.StartDateTime == b.StartDateTime && a.EndDateTime == b.EndDateTime && a.Notes == b.Notes;
    private static State Capture(PlanningAssignment a) => new(a.DriverId, a.Date, a.PlanningDutyId, a.AssignmentType, a.Status,
        a.StartDateTime, a.EndDateTime, a.Notes, a.CreatedUtc, a.UpdatedUtc);
    private static void Restore(PlanningAssignment a, State s)
    {
        a.DriverId = s.DriverId; a.Date = s.Date; a.PlanningDutyId = s.PlanningDutyId; a.AssignmentType = s.AssignmentType;
        a.Status = s.Status; a.StartDateTime = s.Start; a.EndDateTime = s.End; a.Notes = s.Notes; a.CreatedUtc = s.Created; a.UpdatedUtc = s.Updated;
    }
    private sealed record Cell(long SourceId, PlanningAssignment? Existing, PlanningAssignment Desired);
    private sealed record Prepared(LocalPlanningImportPlanDto Plan, PlanningSchedule? Schedule, List<Cell> Cells);
    public sealed record State(Guid DriverId, DateOnly Date, Guid? PlanningDutyId, PlanningAssignmentType AssignmentType,
        PlanningAssignmentStatus Status, DateTime? Start, DateTime? End, string? Notes, DateTime Created, DateTime? Updated);
    public sealed record Change(Guid Id, State? Before, State After);
}
