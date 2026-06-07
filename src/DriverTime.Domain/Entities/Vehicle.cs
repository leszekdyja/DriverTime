using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class Vehicle : BaseEntity
{
    public Guid CompanyId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    public string Vin { get; set; } = string.Empty;

    public bool Active { get; set; } = true;
}
