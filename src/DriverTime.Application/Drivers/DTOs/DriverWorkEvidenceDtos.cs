using DriverTime.Domain.Entities;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DriverTime.Application.Drivers.DTOs;

public class DriverWorkEvidenceMonthDto
{
    public Guid DriverId { get; set; }

    public string DriverFullName { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public DriverWorkEvidenceSummaryDto Summary { get; set; } = new();

    public List<DriverWorkEvidenceDayDto> Days { get; set; } = [];
}

public class DriverWorkEvidenceDayDto
{
    public DateOnly Date { get; set; }

    public int DrivingMinutes { get; set; }

    public int OtherWorkMinutes { get; set; }

    public int AvailabilityMinutes { get; set; }

    public int BreakRestMinutes { get; set; }

    public int AbsenceMinutes { get; set; }

    public int TotalTrackedMinutes { get; set; }

    public List<DriverWorkEvidenceEntryDto> Entries { get; set; } = [];
}

public class DriverWorkEvidenceSummaryDto
{
    public int DrivingMinutes { get; set; }

    public int OtherWorkMinutes { get; set; }

    public int AvailabilityMinutes { get; set; }

    public int BreakRestMinutes { get; set; }

    public int AbsenceMinutes { get; set; }

    public int TotalTrackedMinutes { get; set; }
}

public class DriverWorkEvidenceEntryDto
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    [JsonConverter(typeof(FlexibleTimeOnlyJsonConverter))]
    public TimeOnly StartTime { get; set; }

    [JsonConverter(typeof(FlexibleTimeOnlyJsonConverter))]
    public TimeOnly EndTime { get; set; }

    public bool EndsNextDay { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriverWorkEvidenceActivityType ActivityType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriverWorkEvidenceEntrySource Source { get; set; }

    public string? VehicleRegistration { get; set; }

    public string? CountryCode { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Description { get; set; }

    public int DurationMinutes { get; set; }
}

public class DriverWorkEvidenceEntryRequestDto
{
    public DateOnly Date { get; set; }

    [JsonConverter(typeof(FlexibleTimeOnlyJsonConverter))]
    public TimeOnly StartTime { get; set; }

    [JsonConverter(typeof(FlexibleTimeOnlyJsonConverter))]
    public TimeOnly EndTime { get; set; }

    public bool EndsNextDay { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriverWorkEvidenceActivityType ActivityType { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriverWorkEvidenceEntrySource? Source { get; set; }

    public string? VehicleRegistration { get; set; }

    public string? CountryCode { get; set; }

    public decimal? DistanceKm { get; set; }

    public string? Description { get; set; }
}

public sealed class FlexibleTimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private static readonly string[] Formats =
    [
        "HH:mm",
        "H:mm",
        "HH:mm:ss",
        "H:mm:ss",
        "HH:mm:ss.FFFFFFF",
        "H:mm:ss.FFFFFFF"
    ];

    public override TimeOnly Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        if (TimeOnly.TryParseExact(
            value,
            Formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var time))
        {
            return time;
        }

        throw new JsonException("Nieprawidłowy format godziny. Użyj HH:mm lub HH:mm:ss.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        TimeOnly value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }
}
