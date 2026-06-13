namespace DriverTime.Application.DDD;

public sealed class ParsedCountryEntryDto
{
    public DateTime TimestampUtc { get; set; }

    public string CountryCode { get; set; } = string.Empty;
}