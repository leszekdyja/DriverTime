namespace DriverTime.Domain.Entities;

public class MobileAppInvite
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid DriverId { get; set; }

    public Driver Driver { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UsedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
}
