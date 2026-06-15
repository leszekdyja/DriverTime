namespace DriverTime.Domain.Entities;

public class DddFile
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public Guid? DriverId { get; set; }

    public Driver? Driver { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileHash { get; set; } = string.Empty;

    public bool DriverCreatedDuringImport { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public string DriverCardNumber { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public ICollection<DriverActivity> DriverActivities { get; set; }
        = new List<DriverActivity>();

    public ICollection<VehicleUse> VehicleUses { get; set; }
        = new List<VehicleUse>();

    public ICollection<CountryEntry> CountryEntries { get; set; }
        = new List<CountryEntry>();
}
