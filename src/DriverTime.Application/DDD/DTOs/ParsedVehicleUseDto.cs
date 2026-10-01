using System.Text.Json.Serialization;

namespace DriverTime.Application.DDD.DTOs;

public class ParsedVehicleUseDto
{
    [JsonPropertyName("start")]
    public string Start { get; set; } = string.Empty;

    [JsonPropertyName("end")]
    public string End { get; set; } = string.Empty;

    [JsonPropertyName("vehicle_registration")]
    public string VehicleRegistration { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("start_odometer_km")]
    public int? StartOdometerKm { get; set; }

    [JsonPropertyName("end_odometer_km")]
    public int? EndOdometerKm { get; set; }

    [JsonPropertyName("distance_km")]
    public int? DistanceKm { get; set; }
}
