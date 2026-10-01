namespace DriverTime.Domain.Compliance;

public class ComplianceRuleResult
{
    public string RuleName { get; set; } = string.Empty;

    public List<ComplianceViolationCandidate> Violations { get; set; } = new();
}
