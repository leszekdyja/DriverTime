using System.Globalization;
using System.Text;

namespace DriverTime.Infrastructure.Compliance;

public static class ActivityTypeNormalizer
{
    public const string Driving = "DRIVING";
    public const string Work = "WORK";
    public const string Availability = "AVAILABILITY";
    public const string Rest = "REST";
    public const string Unknown = "UNKNOWN";

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Unknown;
        }

        var normalized = value.Trim().ToUpperInvariant();
        var comparable = NormalizeComparableActivityType(normalized);

        return comparable switch
        {
            "3" or
            "DRIVING" or
            "DRIVE" or
            "JAZDA" or
            "PROWADZENIE" or
            "CARDDRIVERACTIVITYDRIVING" => Driving,

            "2" or
            "WORK" or
            "WORKING" or
            "OTHERWORK" or
            "OTHER_WORK" or
            "PRACA" or
            "INNAPRACA" or
            "CARDDRIVERACTIVITYWORK" => Work,

            "1" or
            "AVAILABILITY" or
            "AVAILABLE" or
            "DYSPOZYCJA" or
            "DYSPOZYCYJNOSC" or
            "POA" or
            "PERIODOFAVAILABILITY" => Availability,

            "0" or
            "REST" or
            "BREAK" or
            "RESTING" or
            "ODPOCZYNEK" or
            "PRZERWA" or
            "PRZERWAODPOCZYNEK" or
            "PAUSE" or
            "CARDDRIVERACTIVITYREST" => Rest,

            _ => Unknown
        };
    }

    private static string NormalizeComparableActivityType(string value)
    {
        var withoutDiacritics = RemoveDiacritics(value);
        var characters = withoutDiacritics
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(characters);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var characters = normalized
            .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark)
            .ToArray();

        return new string(characters).Normalize(NormalizationForm.FormC);
    }
}
