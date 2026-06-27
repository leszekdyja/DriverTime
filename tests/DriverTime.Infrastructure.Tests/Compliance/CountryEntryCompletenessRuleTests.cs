using DriverTime.Domain.Compliance;
using DriverTime.Infrastructure.Compliance;
using DriverTime.Infrastructure.Compliance.Rules;
using DriverTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DriverTime.Infrastructure.Tests.Compliance;

[TestClass]
public class CountryEntryCompletenessRuleTests
{
    private readonly CountryEntryCompletenessRule _rule = new(NullLogger<CountryEntryCompletenessRule>.Instance);

    [TestMethod]
    public void Evaluate_WithActiveDayWithoutCountryEntries_DoesNotCreatePreciseCountryWarnings()
    {
        var driverId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-14T08:00:00Z", "2026-05-14T10:00:00Z")
        };

        var result = _rule.Evaluate(driverId, timeline, Array.Empty<ComplianceCountryEntry>());

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithUnknownCountryEntriesForManyActiveDays_DoesNotCreateDailyIncompleteCountryWarnings()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-15T08:00:00Z", "2026-05-15T12:00:00Z"),
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-16T08:00:00Z", "2026-05-16T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z", "Unknown"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-15T08:00:00Z", "Unknown"),
            CountryEntry(driverId, dddFileId, "CZ", "2026-05-16T08:00:00Z", "Unknown")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
        Assert.IsFalse(result.Violations.Any(x => x.Code == "INCOMPLETE_COUNTRY_DATA"));
        Assert.IsFalse(result.Violations.Any(x => x.Code == "MISSING_END_COUNTRY"));
    }

    [TestMethod]
    public void Evaluate_WithKnownStartWithoutEnd_ReturnsMissingEndWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z", "Start")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_END_COUNTRY", result.Violations[0].Code);
        Assert.AreEqual("Brak kraju zakończenia", result.Violations[0].RuleName);
        Assert.AreEqual("Start", result.Violations[0].Metadata["entryType"]);
    }

    [TestMethod]
    public void Evaluate_WithUnknownAndKnownStartWithoutEnd_ReturnsMissingEndWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-14T09:00:00Z", "Unknown")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_END_COUNTRY", result.Violations[0].Code);
    }

    [TestMethod]
    public void Evaluate_WithKnownEndWithoutStart_ReturnsMissingStartWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "DE", "2026-05-14T16:00:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_START_COUNTRY", result.Violations[0].Code);
        Assert.AreEqual("End", result.Violations[0].Metadata["entryType"]);
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
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-14T16:00:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void CountryEntryModel_EntryTypeDefault_IsUnknown()
    {
        var options = new DbContextOptionsBuilder<DriverTimeDbContext>()
            .UseNpgsql("Host=localhost;Database=drivertime;Username=drivertime;Password=postgres")
            .Options;
        using var dbContext = new DriverTimeDbContext(options);

        var property = dbContext.Model
            .FindEntityType(typeof(DriverTime.Domain.Entities.CountryEntry))
            ?.FindProperty(nameof(DriverTime.Domain.Entities.CountryEntry.EntryType));

        Assert.AreEqual("Unknown", property?.GetDefaultValue());
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
        string entryTimeUtc,
        string entryType)
    {
        return new ComplianceCountryEntry
        {
            SourceCountryEntryId = Guid.NewGuid(),
            DriverId = driverId,
            DddFileId = dddFileId,
            CountryCode = countryCode,
            EntryType = entryType,
            EntryTimeUtc = DateTime.Parse(entryTimeUtc).ToUniversalTime()
        };
    }
}
