using System.Globalization;

namespace DriverTime.Infrastructure.Services;

internal static class VehicleUseMileageKeyBuilder
{
    public static string Build(
        string registrationNumber,
        int? startOdometerKm,
        int? endOdometerKm,
        int? distanceKm,
        DateTime startUtc,
        DateTime endUtc)
    {
        var registration = NormalizeRegistration(registrationNumber);

        if (startOdometerKm.HasValue || endOdometerKm.HasValue)
        {
            return string.Join(
                "|",
                "odometer",
                registration,
                startOdometerKm?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                endOdometerKm?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                distanceKm?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        }

        if (distanceKm.HasValue)
        {
            return string.Join(
                "|",
                "distance-day",
                registration,
                distanceKm.Value.ToString(CultureInfo.InvariantCulture),
                startUtc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return string.Join(
            "|",
            "vehicle-use",
            registration,
            startUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture),
            endUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
    }

    private static string NormalizeRegistration(string registrationNumber) =>
        registrationNumber.Trim().ToUpperInvariant();
}
