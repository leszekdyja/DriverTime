using System.Text.Json;
using System.Text.Json.Serialization;

namespace DriverTime.Application.Planning.DTOs;

public sealed class LocalPlanningSnapshotDto
{
    public int SchemaVersion { get; set; }
    public Guid SourceId { get; set; }
    public DateTime ExportedAtUtc { get; set; }
    public LocalPlanningTablesDto Tables { get; set; } = new();
}

public sealed class LocalPlanningTablesDto
{
    public List<LocalPlanningDriverDto> Drivers { get; set; } = new();
    public List<LocalPlanningDutyDto> Duties { get; set; } = new();
    [JsonPropertyName("planned_schedule")]
    public List<LocalPlanningEntryDto> Entries { get; set; } = new();
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Settings { get; set; } = new();
}

public sealed class LocalPlanningDriverDto
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Details { get; set; } = new();
}

public sealed class LocalPlanningDutyDto
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Details { get; set; } = new();
}

public sealed class LocalPlanningEntryDto
{
    public long Id { get; set; }
    [JsonPropertyName("driver_id")]
    public long DriverId { get; set; }
    [JsonPropertyName("duty_id")]
    public long DutyId { get; set; }
    [JsonPropertyName("plan_date")]
    public DateOnly Date { get; set; }
    [JsonPropertyName("entry_source")]
    public string EntrySource { get; set; } = "";
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Details { get; set; } = new();
}

public sealed class LocalPlanningSyncPreviewDto
{
    public Guid SourceId { get; set; }
    public DateTime ExportedAtUtc { get; set; }
    public int DriversCount { get; set; }
    public int DutiesCount { get; set; }
    public int EntriesCount { get; set; }
    public int ManualEntriesCount { get; set; }
    public int PossibleManualConflicts { get; set; }
    public int PossibleGeneratedConflicts { get; set; }
    public List<string> Months { get; set; } = new();
    public List<LocalPlanningMatchDto> DriverSuggestions { get; set; } = new();
    public List<LocalPlanningMatchDto> DutySuggestions { get; set; } = new();
    public Dictionary<string, int> SettingsCounts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool IsReadOnly => true;
}

public sealed record LocalPlanningMatchDto(long SourceId, string Label, string Status, List<Guid> CandidateIds);
