using System.Text.Json.Serialization;

namespace DriverTime.Application.DDD.DTOs;

public class DddParseResultDto
{
    [JsonPropertyName("parser_name")]
    public string ParserName { get; set; } = string.Empty;

    [JsonPropertyName("parser_version")]
    public string ParserVersion { get; set; } = string.Empty;

    [JsonPropertyName("file_type")]
    public string FileType { get; set; } = string.Empty;

    [JsonPropertyName("card_read_date")]
    public string CardReadDate { get; set; } = string.Empty;

    [JsonPropertyName("activities")]
    public List<ParsedDriverActivityDto> Activities { get; set; } = new();

    [JsonPropertyName("vehicle_uses")]
    public List<ParsedVehicleUseDto> VehicleUses { get; set; } = new();

    [JsonPropertyName("country_code_entries")]
    public List<ParsedCountryEntryDto> CountryCodeEntries { get; set; } = new();
}