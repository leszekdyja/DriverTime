using DriverTime.Domain.Compliance;

namespace DriverTime.Application.Compliance;

public interface IComplianceRule
{
    string Code { get; }

    string Name { get; }

    ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline);
}
