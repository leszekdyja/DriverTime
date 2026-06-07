namespace DriverTime.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public string Vin { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
