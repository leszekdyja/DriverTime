namespace DriverTime.Domain.Entities;

public class CountryEntry
{
    public Guid Id { get; set; }

    public Guid DddFileId { get; set; }

    public DddFile DddFile { get; set; } = null!;

    public string CountryCode { get; set; } = string.Empty;

    public DateTime EntryTimeUtc { get; set; }
}