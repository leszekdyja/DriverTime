namespace DriverTime.Application.DDD.DTOs;

public class DddFileDetailsDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public List<ParsedDriverActivityDto> DriverActivities { get; set; } = new();

    public List<ParsedCountryEntryDto> CountryEntries { get; set; } = new();

    public List<ParsedVehicleUseDto> VehicleUses { get; set; } = new();
}