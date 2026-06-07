using DriverTime.Domain.Common;

namespace DriverTime.Domain.Entities;

public class Driver : BaseEntity
{
    public Guid CompanyId { get; set; }

    public Company? Company { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string DrivingLicenseNumber { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public ICollection<DriverActivity> Activities { get; set; } = new List<DriverActivity>();

    public ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
