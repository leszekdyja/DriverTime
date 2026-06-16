using DriverTime.Infrastructure.Compliance;

namespace DriverTime.Infrastructure.Tests.Compliance;

public class ActivityTypeNormalizerTests
{
    [Theory]
    [InlineData("🚚 Jazda", ActivityTypeNormalizer.Driving)]
    [InlineData("Przerwa / odpoczynek", ActivityTypeNormalizer.Rest)]
    [InlineData("3", ActivityTypeNormalizer.Driving)]
    [InlineData("2", ActivityTypeNormalizer.Work)]
    [InlineData("0", ActivityTypeNormalizer.Rest)]
    public void Normalize_ReturnsCanonicalActivityType(
        string value,
        string expected)
    {
        var result = ActivityTypeNormalizer.Normalize(value);

        Assert.Equal(expected, result);
    }
}
