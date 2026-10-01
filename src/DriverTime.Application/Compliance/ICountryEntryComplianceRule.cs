using DriverTime.Domain.Compliance;

namespace DriverTime.Application.Compliance;

public interface ICountryEntryComplianceRule
{
    string Code { get; }

    string Name { get; }

    ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline,
        IReadOnlyList<ComplianceCountryEntry> countryEntries);
}
