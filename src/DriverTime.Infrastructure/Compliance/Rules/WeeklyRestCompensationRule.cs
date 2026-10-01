using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class WeeklyRestCompensationRule : IComplianceRule
{
    private const string RuleCode = "WEEKLY_REST_COMPENSATION";

    public string Code => RuleCode;

    public string Name => "Weekly rest compensation";

    private readonly ILogger<WeeklyRestCompensationRule> _logger;

    public WeeklyRestCompensationRule(ILogger<WeeklyRestCompensationRule> logger)
    {
        _logger = logger;
    }

    public ComplianceRuleResult Evaluate(
        Guid driverId,
        IReadOnlyList<TimelineActivity> timeline)
    {
        var result = new ComplianceRuleResult
        {
            RuleName = Name
        };

        var validTimeline = timeline
            .Where(x => x.StartUtc < x.EndUtc)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc)
            .ToList();
        var reducedWeeklyRests = WeeklyRestTimelineHelper.BuildWeeklyRestPeriods(validTimeline)
            .Count(WeeklyRestTimelineHelper.IsReducedWeeklyRest);

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: reducedWeeklyRests={ReducedWeeklyRests}, delegatedTo={DelegatedRule}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            reducedWeeklyRests,
            "REDUCED_WEEKLY_REST_COMPENSATION",
            result.Violations.Count);

        return result;
    }
}
