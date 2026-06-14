namespace DriverTime.Domain.Entities;

public class DddFile
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

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