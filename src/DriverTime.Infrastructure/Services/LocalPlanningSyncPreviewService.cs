using System.Globalization;
using System.Text;
using DriverTime.Application.Interfaces;
using DriverTime.Application.Planning.DTOs;
using DriverTime.Application.Planning.Services;
using DriverTime.Domain.Entities;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DriverTime.Infrastructure.Services;

public sealed class LocalPlanningSyncPreviewService(DriverTimeDbContext db, ICurrentUserService currentUser)
    : ILocalPlanningSyncPreviewService
{
    public async Task<LocalPlanningSyncPreviewDto> PreviewAsync(LocalPlanningSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.CompanyId == Guid.Empty || currentUser.IsMobileDriver)
            throw new UnauthorizedAccessException();

        Validate(snapshot);
        var drivers = await db.Drivers.AsNoTracking().Where(x => x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId))
            .ToListAsync(cancellationToken);
        var duties = await db.PlanningDuties.AsNoTracking().Where(x => x.CompanyId == currentUser.CompanyId
            && (!currentUser.OperatingCompanyId.HasValue || x.OperatingCompanyId == currentUser.OperatingCompanyId))
            .ToListAsync(cancellationToken);
        var entries = new List<PlanningAssignment>();
        if (snapshot.Tables.Entries.Count > 0)
        {
            var first = snapshot.Tables.Entries.Min(x => x.Date);
            var last = snapshot.Tables.Entries.Max(x => x.Date);
            var visibleDriverIds = drivers.Select(x => x.Id).ToList();
            entries = await db.PlanningAssignments.AsNoTracking()
                .Where(x => x.CompanyId == currentUser.CompanyId && x.Date >= first && x.Date <= last
                    && visibleDriverIds.Contains(x.DriverId)
                    && x.PlanningSchedule.CompanyId == currentUser.CompanyId
                    && (!currentUser.OperatingCompanyId.HasValue
                        || x.PlanningSchedule.OperatingCompanyId == currentUser.OperatingCompanyId))
                .ToListAsync(cancellationToken);
        }
        return BuildPreview(snapshot, drivers, duties, entries);
    }

    internal static void Validate(LocalPlanningSnapshotDto snapshot)
    {
        if (snapshot.SchemaVersion != 1 || snapshot.SourceId == Guid.Empty
            || snapshot.ExportedAtUtc == default || snapshot.ExportedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Nieobsługiwana wersja eksportu, brak identyfikatora źródła lub daty UTC.");
        var tables = snapshot.Tables;
        if (tables is null || tables.Drivers is null || tables.Duties is null || tables.Entries is null || tables.Settings is null)
            throw new ArgumentException("Eksport nie zawiera wymaganych tabel.");
        if (tables.Drivers.Count > 10000 || tables.Duties.Count > 10000 || tables.Entries.Count > 100000)
            throw new ArgumentException("Eksport jest zbyt duży. Wybierz mniejszy zakres.");
        if (tables.Drivers.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Name))
            || tables.Duties.Any(x => x is null || x.Id <= 0 || string.IsNullOrWhiteSpace(x.Code))
            || tables.Entries.Any(x => x is null || x.Id <= 0 || x.Date == default
                || (x.EntrySource != "manual" && x.EntrySource != "auto")))
            throw new ArgumentException("Nieprawidłowy kierowca, służba, data lub źródło wpisu.");
        if (tables.Drivers.Select(x => x.Id).Distinct().Count() != tables.Drivers.Count
            || tables.Duties.Select(x => x.Id).Distinct().Count() != tables.Duties.Count
            || tables.Entries.Select(x => x.Id).Distinct().Count() != tables.Entries.Count
            || tables.Entries.Select(x => (x.DriverId, x.Date)).Distinct().Count() != tables.Entries.Count)
            throw new ArgumentException("Eksport zawiera powtórzone identyfikatory lub wpisy kierowca–dzień.");
        var driverIds = tables.Drivers.Select(x => x.Id).ToHashSet();
        var dutyIds = tables.Duties.Select(x => x.Id).ToHashSet();
        if (tables.Entries.Any(x => !driverIds.Contains(x.DriverId) || !dutyIds.Contains(x.DutyId)))
            throw new ArgumentException("Wpis grafiku odwołuje się do brakującego kierowcy lub służby.");
    }

    internal static LocalPlanningSyncPreviewDto BuildPreview(LocalPlanningSnapshotDto snapshot,
        IReadOnlyList<Driver> drivers, IReadOnlyList<PlanningDuty> duties, IReadOnlyList<PlanningAssignment> entries)
    {
        var result = new LocalPlanningSyncPreviewDto
        {
            SourceId = snapshot.SourceId, ExportedAtUtc = snapshot.ExportedAtUtc,
            DriversCount = snapshot.Tables.Drivers.Count, DutiesCount = snapshot.Tables.Duties.Count,
            EntriesCount = snapshot.Tables.Entries.Count,
            ManualEntriesCount = snapshot.Tables.Entries.Count(x => x.EntrySource == "manual"),
            Months = snapshot.Tables.Entries.Select(x => x.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture)).Distinct().Order().ToList()
        };
        var driverNames = drivers.SelectMany(x => new[]
            {
                (Name: NormalizeName(x.FirstName + " " + x.LastName), x.Id),
                (Name: NormalizeName(x.LastName), x.Id)
            }).ToLookup(x => x.Name, x => x.Id);
        var dutyCodes = duties.ToLookup(x => x.DutyNumber.Trim(), x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var local in snapshot.Tables.Drivers)
        {
            var name = NormalizeName(local.Name);
            var candidates = driverNames[name].Distinct().ToList();
            result.DriverSuggestions.Add(Match(local.Id, local.Name, candidates));
        }
        foreach (var local in snapshot.Tables.Duties)
            result.DutySuggestions.Add(Match(local.Id, local.Code, dutyCodes[local.Code.Trim()].ToList()));

        var suggestedDrivers = result.DriverSuggestions.Where(x => x.CandidateIds.Count == 1)
            .ToDictionary(x => x.SourceId, x => x.CandidateIds[0]);
        var serverCells = entries.ToLookup(x => (x.DriverId, x.Date));
        foreach (var entry in snapshot.Tables.Entries)
        {
            if (!suggestedDrivers.TryGetValue(entry.DriverId, out var driverId)) continue;
            var overlaps = serverCells[(driverId, entry.Date)].ToList();
            if (overlaps.Any(x => x.Status == PlanningAssignmentStatus.Manual)) result.PossibleManualConflicts++;
            if (overlaps.Any(x => x.Status != PlanningAssignmentStatus.Manual)) result.PossibleGeneratedConflicts++;
        }
        foreach (var (table, rows) in snapshot.Tables.Settings)
            if (rows.ValueKind == System.Text.Json.JsonValueKind.Array) result.SettingsCounts[table] = rows.GetArrayLength();

        result.Warnings.Add("Tylko podgląd. Nie zapisano ani nie usunięto danych. Główna baza pozostaje na komputerze.");
        result.Warnings.Add("Dopasowania są propozycjami, także przy zgodnym nazwisku. Wymagają potwierdzenia i wyboru docelowego grafiku przed importem.");
        result.Warnings.Add("Ustawienia dodatkowe są zachowane w eksporcie, ale nie są jeszcze stosowane na VPS. Ręczny zapis komórek wymaga dopasowań i osobnego zatwierdzenia poniżej.");
        if (suggestedDrivers.Values.Distinct().Count() != suggestedDrivers.Count)
            result.Warnings.Add("Kilku lokalnych kierowców wskazuje tę samą osobę na VPS. Nie wolno ich automatycznie łączyć.");
        if (result.PossibleManualConflicts > 0)
            result.Warnings.Add("Wykryto możliwe kolizje z ręcznymi wpisami na VPS. Nie zostaną automatycznie nadpisane.");
        return result;
    }

    private static LocalPlanningMatchDto Match(long id, string label, List<Guid> candidates) =>
        new(id, label, candidates.Count switch { 0 => "Missing", 1 => "Suggested", _ => "Ambiguous" }, candidates);

    private static string NormalizeName(string value) => string.Join(" ", new string(value.ToUpperInvariant()
        .Replace('Ł', 'L').Normalize(NormalizationForm.FormD)
        .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray())
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));
}
