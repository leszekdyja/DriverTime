using DriverTime.Application.Compliance;
using DriverTime.Domain.Compliance;
using Microsoft.Extensions.Logging;

namespace DriverTime.Infrastructure.Compliance.Rules;

public class BiWeeklyDrivingLimitRule : IComplianceRule
{
    private const string RuleCode = "BI_WEEKLY_DRIVING_LIMIT";
    private const long BiWeeklyLimitMinutes = 90 * 60;
    private static readonly TimeSpan BiWeeklyLimit = TimeSpan.FromMinutes(BiWeeklyLimitMinutes);

    public string Code => RuleCode;

    public string Name => "Bi-weekly driving limit";

    private readonly ILogger<BiWeeklyDrivingLimitRule> _logger;

    public BiWeeklyDrivingLimitRule(ILogger<BiWeeklyDrivingLimitRule> logger)
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

        var weeklyDriving = WeeklyDrivingTimelineHelper.GetDrivingByIsoWeek(timeline);

        if (weeklyDriving.Count == 0)
        {
            LogSummary(driverId, weekCount: 0, pairCount: 0, maxBiWeeklyDriving: TimeSpan.Zero, pairsOverLimit: 0, violationCount: 0);
            return result;
        }

        var weekStarts = weeklyDriving.Keys.OrderBy(x => x).ToList();
        var firstWeekStart = weekStarts[0];
        var lastWeekStart = weekStarts[^1];
        var pairCount = 0;
        var pairsOverLimit = 0;
        var maxBiWeeklyDriving = TimeSpan.Zero;

        for (var weekStart = firstWeekStart; weekStart <= lastWeekStart; weekStart = weekStart.AddDays(7))
        {
            var firstWeekDriving = weeklyDriving.GetValueOrDefault(weekStart);
            var secondWeekDriving = weeklyDriving.GetValueOrDefault(weekStart.AddDays(7));
            var duration = firstWeekDriving + secondWeekDriving;

            pairCount++;
            maxBiWeeklyDriving = Max(maxBiWeeklyDriving, duration);

            if (duration <= BiWeeklyLimit)
            {
                continue;
            }

            pairsOverLimit++;
            var actualMinutes = (long)Math.Round(duration.TotalMinutes);

            result.Violations.Add(new ComplianceViolationCandidate
            {
                Code = RuleCode,
                RuleName = Name,
                Severity = "High",
                Description = $"Czas jazdy w dwóch kolejnych tygodniach wyniósł {FormatDuration(duration)} i przekroczył limit 90 godzin.",
                PeriodStartUtc = weekStart,
                PeriodEndUtc = weekStart.AddDays(14),
                ActualMinutes = actualMinutes,
                LimitMinutes = BiWeeklyLimitMinutes,
                Metadata = new Dictionary<string, long>
                {
                    ["totalDrivingMinutes"] = actualMinutes,
                    ["limitMinutes"] = BiWeeklyLimitMinutes,
                    ["exceededMinutes"] = actualMinutes - BiWeeklyLimitMinutes,
                    ["firstWeekDrivingMinutes"] = (long)Math.Round(firstWeekDriving.TotalMinutes),
                    ["secondWeekDrivingMinutes"] = (long)Math.Round(secondWeekDriving.TotalMinutes)
                }
            });
        }

        LogSummary(
            driverId,
            weeklyDriving.Count,
            pairCount,
            maxBiWeeklyDriving,
            pairsOverLimit,
            result.Violations.Count);

        return result;
    }

    private void LogSummary(
        Guid driverId,
        int weekCount,
        int pairCount,
        TimeSpan maxBiWeeklyDriving,
        int pairsOverLimit,
        int violationCount)
    {
        _logger.LogInformation(
            "Compliance rule {RuleCode} driver {DriverId}: weeks={WeekCount}, consecutivePairs={ConsecutivePairs}, maxBiWeeklyDrivingMinutes={MaxBiWeeklyDrivingMinutes}, pairsOver90h={PairsOverLimit}, violations={ViolationCount}.",
            RuleCode,
            driverId,
            weekCount,
            pairCount,
            (long)Math.Round(maxBiWeeklyDriving.TotalMinutes),
            pairsOverLimit,
            violationCount);
    }

    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalHours} godz. {duration.Minutes} min";

    private static TimeSpan Max(TimeSpan left, TimeSpan right) =>
        left >= right ? left : right;
}
