namespace DriverTime.Application.DDD.DTOs;

public class DddParseResultDto
{
    public string ParserName { get; set; } = string.Empty;

    public string ParserVersion { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public DateTime? CardReadDate { get; set; }

    public List<ParsedDriverActivityDto> Activities { get; set; } = new();

    public List<ParsedVehicleUseDto> VehicleUses { get; set; } = new();

    public List<ParsedCountryEntryDto> CountryCodeEntries { get; set; } = new();
}
