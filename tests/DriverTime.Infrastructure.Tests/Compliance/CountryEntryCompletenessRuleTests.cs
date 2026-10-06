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
    public void Evaluate_WithActiveDayWithoutCountryEntries_DoesNotCreatePreciseCountryWarningsBecauseThereIsNoReliableStartOrEndEvidence()
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
    public void Evaluate_WithUnknownAndKnownEndWithoutStart_ReturnsMissingStartWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T09:00:00Z", "Unknown"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-14T16:00:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_START_COUNTRY", result.Violations[0].Code);
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
    public void Evaluate_WithUnknownAndStartAndEndCountryEntries_ReturnsNoWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-14T08:00:00Z", "2026-05-14T10:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T08:00:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-14T11:00:00Z", "Unknown"),
            CountryEntry(driverId, dddFileId, "CZ", "2026-05-14T16:00:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithCountryEntriesOutsideActiveDays_ReturnsNoWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-13T08:00:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-15T16:00:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
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
    public void Evaluate_WithWorkPeriodCrossingMidnight_PairsStartAndEndAcrossCalendarDays()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-14T22:00:00Z", "2026-05-15T02:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-05-14T21:45:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "DE", "2026-05-15T02:15:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithCardLeftInTachographForMoreThan24Hours_PairsStartAndEnd()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-09-20T12:30:00Z", "2026-09-22T05:10:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "PL", "2026-09-20T12:30:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "PL", "2026-09-22T03:36:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithInvalidStartCountryAndFollowingEnd_ReturnsOnlyInvalidCodeWarning()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-09-19T07:35:00Z", "2026-09-20T14:30:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "---", "2026-09-19T07:35:00Z", "Start"),
            CountryEntry(driverId, dddFileId, "PL", "2026-09-20T11:40:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("INVALID_COUNTRY_CODE", result.Violations[0].Code);
    }

    [TestMethod]
    public void Evaluate_WithSameStartAndEndImportedFromSeveralDddFiles_ReturnsNoWarning()
    {
        var driverId = Guid.NewGuid();
        var firstDddFileId = Guid.NewGuid();
        var secondDddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-14T08:00:00Z", "2026-05-14T16:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, firstDddFileId, "PL", "2026-05-14T07:45:00Z", "Start"),
            CountryEntry(driverId, secondDddFileId, "pl", "2026-05-14T07:45:00Z", "Start"),
            CountryEntry(driverId, firstDddFileId, "PL", "2026-05-14T16:15:00Z", "End"),
            CountryEntry(driverId, secondDddFileId, "PL", "2026-05-14T16:15:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithSameUnpairedStartImportedFromSeveralDddFiles_ReturnsOneWarning()
    {
        var driverId = Guid.NewGuid();
        var firstDddFileId = Guid.NewGuid();
        var secondDddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, firstDddFileId, "PL", "2026-05-14T07:45:00Z", "Start"),
            CountryEntry(driverId, secondDddFileId, "PL", "2026-05-14T07:45:00Z", "Start")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_END_COUNTRY", result.Violations[0].Code);
    }

    [TestMethod]
    public void Evaluate_WithSameInvalidEntryImportedFromSeveralDddFiles_ReturnsOneWarning()
    {
        var driverId = Guid.NewGuid();
        var firstDddFileId = Guid.NewGuid();
        var secondDddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Work, "2026-05-14T08:00:00Z", "2026-05-14T12:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, firstDddFileId, "---", "2026-05-14T07:45:00Z", "Start"),
            CountryEntry(driverId, secondDddFileId, " --- ", "2026-05-14T07:45:00Z", "Start")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("INVALID_COUNTRY_CODE", result.Violations[0].Code);
    }

    [TestMethod]
    public void Evaluate_WithValidAndInvalidCopiesOfSameEvents_PrefersValidCountryCodes()
    {
        var driverId = Guid.NewGuid();
        var legacyDddFileId = Guid.NewGuid();
        var currentDddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-09-11T03:10:00Z", "2026-09-11T17:30:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, legacyDddFileId, "---", "2026-09-11T02:56:40Z", "Start"),
            CountryEntry(driverId, currentDddFileId, "PL", "2026-09-11T02:56:40Z", "Start"),
            CountryEntry(driverId, legacyDddFileId, "0x00", "2026-09-11T17:49:09Z", "End"),
            CountryEntry(driverId, currentDddFileId, "PL", "2026-09-11T17:49:09Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(0, result.Violations.Count);
    }

    [TestMethod]
    public void Evaluate_WithGenuineMissingStart_UsesEndEntryTimeInsteadOfMidnight()
    {
        var driverId = Guid.NewGuid();
        var dddFileId = Guid.NewGuid();
        var timeline = new[]
        {
            Activity(driverId, ActivityTypeNormalizer.Driving, "2026-05-15T01:00:00Z", "2026-05-15T02:00:00Z")
        };
        var countryEntries = new[]
        {
            CountryEntry(driverId, dddFileId, "DE", "2026-05-15T02:15:00Z", "End")
        };

        var result = _rule.Evaluate(driverId, timeline, countryEntries);

        Assert.AreEqual(1, result.Violations.Count);
        Assert.AreEqual("MISSING_START_COUNTRY", result.Violations[0].Code);
        Assert.AreEqual(DateTime.Parse("2026-05-15T02:15:00Z").ToUniversalTime(), result.Violations[0].PeriodStartUtc);
        StringAssert.Contains(result.Violations[0].Description, "2026-05-15 04:15 czasu polskiego");
        Assert.IsFalse(result.Violations[0].Description.Contains("UTC", StringComparison.Ordinal));
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
