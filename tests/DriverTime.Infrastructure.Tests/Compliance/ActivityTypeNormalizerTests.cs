using DriverTime.Infrastructure.Compliance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class ActivityTypeNormalizerTests
{
    [DataTestMethod]
    [DataRow("Jazda", ActivityTypeNormalizer.Driving)]
    [DataRow("Przerwa / odpoczynek", ActivityTypeNormalizer.Rest)]
    [DataRow("Manual break", ActivityTypeNormalizer.Rest)]
    [DataRow("manual rest", ActivityTypeNormalizer.Rest)]
    [DataRow("Wpis manualny - przerwa", ActivityTypeNormalizer.Rest)]
    [DataRow("Manual availability", ActivityTypeNormalizer.Availability)]
    [DataRow("3", ActivityTypeNormalizer.Driving)]
    [DataRow("2", ActivityTypeNormalizer.Work)]
    [DataRow("0", ActivityTypeNormalizer.Rest)]
    public void Normalize_ReturnsCanonicalActivityType(
        string value,
        string expected)
    {
        var result = ActivityTypeNormalizer.Normalize(value);

        Assert.AreEqual(expected, result);
    }
}
