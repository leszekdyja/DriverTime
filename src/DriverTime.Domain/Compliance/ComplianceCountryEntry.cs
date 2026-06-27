namespace DriverTime.Domain.Compliance;

public class ComplianceCountryEntry
{
    public Guid SourceCountryEntryId { get; set; }

    public Guid DriverId { get; set; }

    public Guid DddFileId { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string EntryType { get; set; } = "Unknown";

    public DateTime EntryTimeUtc { get; set; }
}
