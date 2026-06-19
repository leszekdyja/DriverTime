using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class WeeklyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "WEEKLY_DRIVING_LIMIT";
    private const long WeeklyLimitMinutes = 56 * 60;
    private static readonly TimeSpan WeeklyLimit = TimeSpan.FromMinutes(WeeklyLimitMinutes);

    public string Code => RuleCode;

    public string Name => "Weekly driving limit";

    private readonly ILogger<WeeklyDrivingLimitRule> _logger;

    public WeeklyDrivingLimitRule(ILogger<WeeklyDrivingLimitRule> logger)
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

        var weeklyDriving = WeeklyDrivingTimelineHelper.GetDrivingByIsoWeek(timeline)
            .Select(x => new
            {
                WeekStart = x.Key,
                Duration = x.Value
            })
            .OrderBy(x => x.WeekStart)
            .ToList();

        foreach (var week in weeklyDriving)
        {
            if (week.Duration <= WeeklyLimit)
            {
                continue;
            }

            var actualMinutes = (long)Math.Round(week.Duration.TotalMinutes);

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Tygodniowy czas jazdy wyniósł {FormatDuration(week.Duration)} i przekroczył limit 56 godzin.",
                PeriodStartUtc = week.WeekStart,
                PeriodEndUtc = week.WeekStart.AddDays(7),
                ActualMinutes = actualMinutes,
                LimitMinutes = WeeklyLimitMinutes,
                Metadata = new Dictionary<string, object>
                {
                    ["totalDrivingMinutes"] = actualMinutes,
                    ["limitMinutes"] = WeeklyLimitMinutes,
                    ["exceededMinutes"] = actualMinutes - WeeklyLimitMinutes
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, maxWeeklyDrivingMinutes={MaxWeeklyDrivingMinutes}, weeksOver56h={WeeksOverLimit}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weeklyDriving.Count,
            weeklyDriving.Count == 0 ? 0 : (long)Math.Round(weeklyDriving.Max(x => x.Duration.TotalMinutes)),
            weeklyDriving.Count(x => x.Duration > WeeklyLimit),
            result.Violations.Count);

        return result;
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
