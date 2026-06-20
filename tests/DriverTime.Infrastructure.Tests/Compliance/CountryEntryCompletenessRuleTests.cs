using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class CountryEntryCompletenessRuleTests
{
    private readonly CountryEntryCompletenessRule _rule = new(NullLogger<CountryEntryCompletenessRule>.Instance);

    [TestMethod]
    public void Evaluate_WithActiveDayWithoutCountryEntries_ReturnsMissingStartAndEndWarnings()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-14T08:00:00Z", "2026-05-14T10:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline, Array.Empty<ComplianceCountryEntry>());

        Assert.AreEqual(2, result.Violations.Count);
        Assert.IsTrue(result.Violations.Any(x => x.Code == "MISSING_START_COUNTRY"));
        Assert.IsTrue(result.Violations.Any(x => x.Code == "MISSING_END_COUNTRY"));
        Assert.IsTrue(result.Violations.All(x => x.Severity == "Warning"));
    }

    [TestMethod]
    public void Evaluate_WithOnlyOneCountryEntryForActiveDay_ReturnsMissingEndWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_END_COUNTRY", result.Violations[0].Code);
        Assert.AreEqual("Brak kraju zakończenia", result.Violations[0].RuleName);
    }

    [TestMethod]
    public void Evaluate_WithStartAndEndCountryEntriesForActiveDay_ReturnsNoWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-14T08:00:00Z", "2026-05-14T10:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T15:00:00Z", "2026-05-14T16:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-14T16:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    private static TimelineActivity Activity(
        Guid driverId,
        string activityType,
        string startUtc,
        string endUtc)
    {
        return new TimelineActivity
        {
            SourceActivityId = Guid.NewGuid(),
            DriverId = driverId,
            ActivityType = activityType,
            StartUtc = DateTime.Parse(startUtc).ToUniversalTime(),
            EndUtc = DateTime.Parse(endUtc).ToUniversalTime()
        };
    }

    private static ComplianceCountryEntry CountryEntry(
        Guid driverId,
        Guid dddFileId,
        string countryCode,
        string entryTimeUtc)
    {
        return new ComplianceCountryEntry
        {
            SourceCountryEntryId = Guid.NewGuid(),
            DriverId = driverId,
            DddFileId = dddFileId,
            CountryCode = countryCode,
            EntryTimeUtc = DateTime.Parse(entryTimeUtc).ToUniversalTime()
        };
    }
}
