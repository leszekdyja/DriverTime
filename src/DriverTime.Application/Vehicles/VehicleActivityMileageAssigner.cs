namespace DriverTime.Application.Vehicles;

public sealed record VehicleActivityMileageSource(
    Guid ActivityId,
    Guid VehicleUseId,
    string RegistrationNumber,
    string ActivityType,
    DateTime ActivityStartUtc,
    DateTime ActivityEndUtc,
    DateTime VehicleUseStartUtc,
    DateTime VehicleUseEndUtc,
    int? StartOdometerKm,
    int? EndOdometerKm,
    int? DistanceKm);

public sealed record VehicleActivityMileage(int? StartOdometerKm, int? EndOdometerKm, int? DistanceKm);

public static class VehicleActivityMileageAssigner
{
    public static IReadOnlyDictionary<string, VehicleActivityMileage> Assign(IEnumerable<VehicleActivityMileageSource> sources)
    {
        var assignments = new Dictionary<string, VehicleActivityMileage>();

        foreach (var group in sources
            .Where(CanReportMileage)
            .GroupBy(BuildMileageKey))
        {
            var candidates = group
                .Where(CoversEntireVehicleUse)
                .OrderBy(x => x.ActivityStartUtc)
                .ThenBy(x => x.ActivityEndUtc)
                .ThenBy(x => x.ActivityId)
                .ToList();

            if (candidates.Count != 1)
            {
                continue;
            }

            var source = candidates[0];
            assignments[BuildAssignmentKey(source.ActivityId, source.VehicleUseId)] = new VehicleActivityMileage(
                source.StartOdometerKm,
                source.EndOdometerKm,
                source.DistanceKm);
        }

        return assignments;
    }

    public static string BuildAssignmentKey(Guid activityId, Guid vehicleUseId) =>
        $"{activityId:D}|{vehicleUseId:D}";

    private static bool CanReportMileage(VehicleActivityMileageSource source) =>
        source.ActivityType.Equals("DRIVING", StringComparison.OrdinalIgnoreCase)
        && (source.DistanceKm.HasValue || source.StartOdometerKm.HasValue || source.EndOdometerKm.HasValue);

    private static bool CoversEntireVehicleUse(VehicleActivityMileageSource source) =>
        source.ActivityStartUtc <= source.VehicleUseStartUtc
        && source.ActivityEndUtc >= source.VehicleUseEndUtc;

    private static string BuildMileageKey(VehicleActivityMileageSource source) => string.Join(
        "|",
        NormalizeRegistration(source.RegistrationNumber),
        source.VehicleUseStartUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
        source.VehicleUseEndUtc.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
        source.StartOdometerKm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        source.EndOdometerKm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        source.DistanceKm?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);

    private static string NormalizeRegistration(string registrationNumber) =>
        new string(registrationNumber
            .Where(x => !char.IsWhiteSpace(x))
            .ToArray())
            .ToUpperInvariant();
}
