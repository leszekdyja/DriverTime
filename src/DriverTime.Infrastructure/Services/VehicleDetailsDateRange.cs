namespace DriverTime.Infrastructure.Services;

public sealed class VehicleDetailsDateRange
{
    private VehicleDetailsDateRange(DateTime? fromUtc, DateTime? toExclusiveUtc)
    {
        FromUtc = fromUtc;
        ToExclusiveUtc = toExclusiveUtc;
    }

    public DateTime? FromUtc { get; }

    public DateTime? ToExclusiveUtc { get; }

    public bool IsValid =>
        !FromUtc.HasValue
        || !ToExclusiveUtc.HasValue
        || FromUtc.Value < ToExclusiveUtc.Value;

    public static VehicleDetailsDateRange Create(DateOnly? from, DateOnly? to)
    {
        var fromUtc = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusiveUtc = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return new VehicleDetailsDateRange(fromUtc, toExclusiveUtc);
    }

    public bool Overlaps(DateTime startUtc, DateTime endUtc)
    {
        return (!FromUtc.HasValue || endUtc > FromUtc.Value)
            && (!ToExclusiveUtc.HasValue || startUtc < ToExclusiveUtc.Value);
    }

    public DateTime ClipStart(DateTime startUtc)
    {
        return FromUtc.HasValue && startUtc < FromUtc.Value
            ? FromUtc.Value
            : startUtc;
    }

    public DateTime ClipEnd(DateTime endUtc)
    {
        return ToExclusiveUtc.HasValue && endUtc > ToExclusiveUtc.Value
            ? ToExclusiveUtc.Value
            : endUtc;
    }
}