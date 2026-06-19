using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class DailyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "DAILY_DRIVING_LIMIT";
    private static readonly TimeSpan StandardDailyLimit = TimeSpan.FromHours(9);
    private static readonly TimeSpan ExtendedDailyLimit = TimeSpan.FromHours(10);

    public string Code => RuleCode;

    public string Name => "Daily driving limit";

    private readonly ILogger<DailyDrivingLimitRule> _logger;

    public DailyDrivingLimitRule(ILogger<DailyDrivingLimitRule> logger)
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

        var dailyDriving = WeeklyDrivingTimelineHelper.GetDrivingByUtcDay(timeline)
            .Select(x => new
            {
                Day = x.Key,
                TotalDriving = x.Value
            })
            .OrderBy(x => x.Day)
            .ToList();

        foreach (var day in dailyDriving)
        {
            if (day.TotalDriving <= StandardDailyLimit)
            {
                continue;
            }

            var severity = day.TotalDriving > ExtendedDailyLimit
                ? "HIGH"
                : "MEDIUM";
            var limit = day.TotalDriving > ExtendedDailyLimit
                ? ExtendedDailyLimit
                : StandardDailyLimit;
            var exceededMinutes = Math.Max(
                0,
                (long)Math.Round((day.TotalDriving - StandardDailyLimit).TotalMinutes));

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = severity,
                Description = BuildMessage(day.TotalDriving, severity),
                PeriodStartUtc = day.Day,
                PeriodEndUtc = day.Day.AddDays(1),
                ActualMinutes = (long)Math.Round(day.TotalDriving.TotalMinutes),
                LimitMinutes = (long)limit.TotalMinutes,
                Metadata = new Dictionary<string, object>
                {
                    ["totalDrivingMinutes"] = (long)Math.Round(day.TotalDriving.TotalMinutes),
                    ["exceededMinutes"] = exceededMinutes
                }
            });
        }

        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: drivingDays={DrivingDays}, maxDailyDrivingMinutes={MaxDailyDrivingMinutes}, daysOver9h={DaysOverStandardLimit}, daysOver10h={DaysOverExtendedLimit}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            dailyDriving.Count,
            dailyDriving.Count == 0 ? 0 : (long)Math.Round(dailyDriving.Max(x => x.TotalDriving.TotalMinutes)),
            dailyDriving.Count(x => x.TotalDriving > StandardDailyLimit),
            dailyDriving.Count(x => x.TotalDriving > ExtendedDailyLimit),
            result.Violations.Count);

        return result;
    }

    private static string BuildMessage(TimeSpan totalDriving, string severity)
    {
        var formattedDuration = FormatDuration(totalDriving);

        return severity == "HIGH"
            ? $"Dzienny czas jazdy wyniósł {formattedDuration} i przekroczył limit 10 godzin."
            : $"Dzienny czas jazdy wyniósł {formattedDuration} i przekroczył standardowy limit 9 godzin.";
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";
}
