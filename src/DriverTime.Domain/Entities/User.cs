using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class User : BaseEntity
{
    public Guid CompanyId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; } = true;
}
