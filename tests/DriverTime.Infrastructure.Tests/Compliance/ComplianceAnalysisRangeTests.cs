using DriverTime.Infrastructure.Compliance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class ComplianceAnalysisRangeTests
{
    [TestMethod]
    public void Resolve_WithoutRequestedRange_UsesLast60Days()
    {
        var nowUtc = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);

        var range = ComplianceAnalysisRange.Resolve(nowUtc, null, null);

        Assert.AreEqual(nowUtc.AddDays(-60), range.VisibleStartUtc);
        Assert.AreEqual(nowUtc, range.VisibleEndUtc);
    }

    [TestMethod]
    public void Resolve_WithRequestedRange_RespectsRequestedRange()
    {
        var nowUtc = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var startUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var range = ComplianceAnalysisRange.Resolve(nowUtc, startUtc, endUtc);

        Assert.AreEqual(startUtc, range.VisibleStartUtc);
        Assert.AreEqual(endUtc, range.VisibleEndUtc);
    }

    [TestMethod]
    public void Resolve_AddsTechnicalBufferBeforeVisibleStartOnly()
    {
        var nowUtc = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var startUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        var range = ComplianceAnalysisRange.Resolve(nowUtc, startUtc, endUtc);

        Assert.AreEqual(startUtc.AddDays(-ComplianceAnalysisRange.TechnicalBufferDays), range.QueryStartUtc);
        Assert.AreEqual(endUtc, range.QueryEndUtc);
    }

    [TestMethod]
    public void Resolve_RangeShorterThan60Days_KeepsShorterRange()
    {
        var nowUtc = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc);
        var startUtc = new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);

        var range = ComplianceAnalysisRange.Resolve(nowUtc, startUtc, endUtc);

        Assert.AreEqual(startUtc, range.VisibleStartUtc);
        Assert.AreEqual(endUtc, range.VisibleEndUtc);
    }

    [TestMethod]
    public void IntersectsVisibleRange_BufferPeriodBeforeVisibleStart_ReturnsFalse()
    {
        var range = ComplianceAnalysisRange.Resolve(
            new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));

        var periodStartUtc = range.QueryStartUtc;
        var periodEndUtc = range.VisibleStartUtc.AddMinutes(-1);

        Assert.IsFalse(range.IntersectsVisibleRange(periodStartUtc, periodEndUtc));
    }
}
