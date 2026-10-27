namespace DriverTime.Application.Planning.DTOs;

public sealed record LocalPlanningOptionDto(Guid Id, string Label);
public sealed class LocalPlanningImportOptionsDto
{
    public Guid? OperatingCompanyId { get; set; }
    public bool CanChooseCompany { get; set; }
    public List<LocalPlanningOptionDto> Companies { get; set; } = new();
    public List<LocalPlanningOptionDto> Drivers { get; set; } = new();
    public List<LocalPlanningOptionDto> Duties { get; set; } = new();
    public List<LocalPlanningScheduleOptionDto> Schedules { get; set; } = new();
}
public sealed record LocalPlanningScheduleOptionDto(Guid Id, string Label, int Year, int Month);

public sealed class LocalPlanningImportRequestDto
{
    public LocalPlanningSnapshotDto Snapshot { get; set; } = new();
    public Guid OperationId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid? OperatingCompanyId { get; set; }
    public Guid? ScheduleId { get; set; }
    public Dictionary<long, Guid> DriverMappings { get; set; } = new();
    public Dictionary<long, Guid> DutyMappings { get; set; } = new();
    public Dictionary<long, string> Decisions { get; set; } = new();
    public bool AllowSkipping { get; set; }
    public bool AcknowledgeDefinitions { get; set; }
    public string RevisionToken { get; set; } = "";
}

public sealed record LocalPlanningImportConflictDto(long SourceEntryId, DateOnly Date, string Driver,
    string LocalDuty, string ServerDuty, string ServerStatus, string? Decision);
public sealed class LocalPlanningImportPlanDto
{
    public string RevisionToken { get; set; } = "";
    public Guid? ScheduleId { get; set; }
    public int SelectedEntries { get; set; }
    public int SkippedEntries { get; set; }
    public int NewEntries { get; set; }
    public int UnchangedEntries { get; set; }
    public List<LocalPlanningImportConflictDto> Conflicts { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public bool CanApply { get; set; }
}
public sealed record LocalPlanningImportResultDto(Guid BatchId, Guid ScheduleId, int Created, int Updated,
    int KeptServer, int Unchanged, int Skipped);

public sealed record LocalPlanningBatchListItemDto(Guid Id, Guid ScheduleId, int Year, int Month,
    DateTime CreatedAtUtc, DateTime? RevertedAtUtc);
