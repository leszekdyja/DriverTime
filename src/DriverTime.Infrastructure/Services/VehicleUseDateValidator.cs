namespace DriverTime.Infrastructure.Services;

public static class VehicleUseDateValidator
{
    public static readonly DateTime MinimumStartUtc = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static bool IsValid(DateTime startUtc, DateTime endUtc, DateTime nowUtc)
    {
        var latestAllowedUtc = nowUtc.AddDays(1);

        return startUtc >= MinimumStartUtc
            && endUtc > startUtc
            && startUtc <= latestAllowedUtc
            && endUtc <= latestAllowedUtc;
    }
}
