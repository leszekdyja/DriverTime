using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class RegularWeeklyRestRule : IComplianceRule
{
    private const string RuleCode = "REGULAR_WEEKLY_REST";

    public string Code => RuleCode;

    public string Name => "Regular weekly rest";

    private readonly ILogger<RegularWeeklyRestRule> _logger;

    public RegularWeeklyRestRule(ILogger<RegularWeeklyRestRule> logger)
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
        var weeklyRests = WeeklyRestTimelineHelper.BuildWeeklyRestPeriods(validTimeline);
        var regularWeeklyRests = weeklyRests.Count(WeeklyRestTimelineHelper.IsRegularWeeklyRest);
        var reducedWeeklyRests = weeklyRests.Count(WeeklyRestTimelineHelper.IsReducedWeeklyRest);

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeklyRests={WeeklyRests}, regularWeeklyRests={RegularWeeklyRests}, reducedWeeklyRests={ReducedWeeklyRests}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weeklyRests.Count,
            regularWeeklyRests,
            reducedWeeklyRests,
            result.Violations.Count);

        return result;
    }
}
