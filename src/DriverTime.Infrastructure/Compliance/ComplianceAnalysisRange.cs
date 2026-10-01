namespace DriverTime.Infrastructure.Compliance;

public sealed record ComplianceAnalysisRange(
    DateTime VisibleStartUtc,
    DateTime VisibleEndUtc,
    DateTime QueryStartUtc,
    DateTime QueryEndUtc)
{
    public const int DefaultVisibleDays = 60;
    public const int TechnicalBufferDays = 28;

    public static ComplianceAnalysisRange Resolve(
        DateTime nowUtc,
        DateTime? requestedStartUtc,
        DateTime? requestedEndUtc)
    {
        var visibleEndUtc = EnsureUtc(requestedEndUtc ?? nowUtc);
        var visibleStartUtc = EnsureUtc(requestedStartUtc ?? visibleEndUtc.AddDays(-DefaultVisibleDays));

        if (visibleEndUtc <= visibleStartUtc)
        {
            visibleEndUtc = visibleStartUtc.AddDays(1);
        }

        return new ComplianceAnalysisRange(
            visibleStartUtc,
            visibleEndUtc,
            visibleStartUtc.AddDays(-TechnicalBufferDays),
            visibleEndUtc);
    }

    public bool IntersectsVisibleRange(DateTime periodStartUtc, DateTime periodEndUtc)
    {
        return periodEndUtc >= VisibleStartUtc
            && periodStartUtc <= VisibleEndUtc;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
