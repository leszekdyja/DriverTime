using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string VatNumber { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
