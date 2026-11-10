using DriverTime.Application.DDD.DTOs;

namespace DriverTime.Infrastructure.Services;

internal static class VehicleVinMatcher
{
    internal static string RegistrationKey(string? value) =>
        string.Concat((value ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();

    internal static string NormalizeVin(string? value)
    {
        var vin = (value ?? "").Trim().ToUpperInvariant();
        return vin.Length == 17 && vin.All(c => char.IsAsciiLetterOrDigit(c) && c is not ('I' or 'O' or 'Q'))
            ? vin : string.Empty;
    }

    internal static Dictionary<string, string> UniqueEvidence(IEnumerable<ParsedVehicleUseDto> uses)
    {
        var valid = uses
        .Select(x => new {Key = RegistrationKey(x.VehicleRegistration), Vin = NormalizeVin(x.VehicleIdentificationNumber)})
        .Where(x => x.Key.Length >= 5 && x.Vin.Length == 17)
        .ToList();
        return valid
        .Where(x => valid.Where(y => y.Vin == x.Vin).Select(y => y.Key).Distinct().Count() == 1)
        .GroupBy(x => x.Key)
        .Where(g => g.Select(x => x.Vin).Distinct().Count() == 1)
        .ToDictionary(g => g.Key, g => g.First().Vin);
    }
}
