namespace DriverTime.Application.DDD.DTOs;

public class DddFileDetailsDto
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string DriverCardNumber { get; set; } = string.Empty;

    public string DriverFirstName { get; set; } = string.Empty;

    public string DriverLastName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }

    public List<ParsedDriverActivityDto> Activities { get; set; } = [];

    public List<ParsedVehicleUseDto> VehicleUses { get; set; } = [];

    public List<ParsedCountryEntryDto> CountryEntries { get; set; } = [];
}