using DriverTime.Domain.Entities;

namespace DriverTime.Infrastructure.Services;

public static class VehicleActivityDriverAssignment
{
    public static bool HasOverlap(DateTime activityStartUtc, DateTime activityEndUtc, DateTime vehicleUseStartUtc, DateTime vehicleUseEndUtc) =>
        activityStartUtc < vehicleUseEndUtc && activityEndUtc > vehicleUseStartUtc;

    public static string? ResolveDriverName(
        DateTime activityStartUtc,
        DateTime activityEndUtc,
        VehicleUse vehicleUse,
        Func<string, string, string> formatDriverName)
    {
        if (!HasOverlap(activityStartUtc, activityEndUtc, vehicleUse.StartUtc, vehicleUse.EndUtc) ||
            vehicleUse.DddFile?.DriverId is null ||
            vehicleUse.DddFile.Driver is null)
        {
            return null;
        }

        var driverName = formatDriverName(
            vehicleUse.DddFile.Driver.FirstName,
            vehicleUse.DddFile.Driver.LastName);

        return string.IsNullOrWhiteSpace(driverName) ? null : driverName;
    }
}
