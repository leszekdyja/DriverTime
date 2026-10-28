using System.Globalization;
using System.Text.RegularExpressions;

namespace DriverTime.Infrastructure.Services;

internal static class CardReadDateResolver
{
    internal sealed record ReadDate(DateTime AtUtc, string Source);
    private static readonly Regex FileStamp = new(@"(?<!\d)(20\d{6})[_-]?(\d{6})(?!\d)", RegexOptions.CultureInvariant);

    internal static ReadDate? Resolve(DateTime? storedUtc, string? storedSource, string originalFileName, DateTime nowUtc)
    {
        if (storedUtc.HasValue && storedSource is "CardDownload" or "OriginalFileName"
            && Valid(storedUtc.Value, nowUtc))
            return new(DateTime.SpecifyKind(storedUtc.Value, DateTimeKind.Utc), storedSource);
        var stamp = FileStamp.Match(Path.GetFileName(originalFileName));
        if (!stamp.Success || !DateTime.TryParseExact(stamp.Groups[1].Value + stamp.Groups[2].Value,
                "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return null;
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) return null;
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        return Valid(utc, nowUtc) ? new(utc, "OriginalFileName") : null;
    }

    internal static ReadDate? FromParser(string? value, string? source, string originalFileName, DateTime nowUtc)
    {
        DateTime? parsed = source == "CardDownload" && DateTimeOffset.TryParse(value,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            ? timestamp.UtcDateTime : null;
        return Resolve(parsed, parsed.HasValue ? "CardDownload" : null, originalFileName, nowUtc);
    }

    private static bool Valid(DateTime value, DateTime nowUtc) => value.Year >= 1990 && value <= nowUtc;
}
