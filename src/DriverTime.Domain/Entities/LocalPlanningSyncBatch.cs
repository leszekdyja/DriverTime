namespace DriverTime.Domain.Entities;

public sealed class LocalPlanningSyncBatch
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? OperatingCompanyId { get; set; }
    public Guid SourceId { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanningScheduleId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string RequestHash { get; set; } = "";
    public string RequestJson { get; set; } = "";
    public string BackupJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevertedAtUtc { get; set; }
}
