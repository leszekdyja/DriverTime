namespace DriverTime.Domain.Entities;

public class OperatingCompany
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string TaxNumber { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    public ICollection<User> Users { get; set; } = new List<User>();
}
