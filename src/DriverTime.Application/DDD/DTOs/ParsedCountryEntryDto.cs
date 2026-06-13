namespace DriverTime.Application.DDD.DTOs;

public class ParsedCountryEntryDto
{
    public DateTime Timestamp { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string RelatedDay { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}
