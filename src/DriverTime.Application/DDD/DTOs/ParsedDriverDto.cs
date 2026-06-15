using System.Text.Json.Serialization;

namespace DriverTime.Application.DDD.DTOs;

public class ParsedDriverDto
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("card_number")]
    public string CardNumber { get; set; } = string.Empty;

    [JsonPropertyName("card_expiry_date")]
    public string CardExpiryDate { get; set; } = string.Empty;

    [JsonPropertyName("card_issuing_country")]
    public string CardIssuingCountry { get; set; } = string.Empty;
}
