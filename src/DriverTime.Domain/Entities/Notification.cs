using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;
}
