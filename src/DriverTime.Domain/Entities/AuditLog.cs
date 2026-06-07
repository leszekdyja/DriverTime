using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }
}
