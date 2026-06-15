using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class User : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = null!;

    public bool Active { get; set; } = true;
}
