namespace DriverTime.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
