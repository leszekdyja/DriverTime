using DriverTime.Domain.Entities;

namespace DriverTime.Application.Planning;

public static class PlanningAssignmentConstraintEvaluator
{
    public const decimal PreferredDutyScore = -900m;
    public const decimal FixedDutyScore = -100000m;

    public static PlanningAssignmentConstraintEvaluation Evaluate(
        Guid companyId,
        Driver driver,
        PlanningDuty duty,
        DateOnly date,
        IEnumerable<PlanningAssignmentRule> rules)
    {
        var result = new PlanningAssignmentConstraintEvaluation();
        var dutyNumber = NormalizeDutyNumber(duty.DutyNumber);
        var applicableRules = rules
            .Where(rule => MatchesCompany(rule, companyId) && MatchesDate(rule, date) && MatchesDuty(rule, duty, dutyNumber))
            .ToList();
        var fixedRules = applicableRules.Where(rule => rule.Type == PlanningAssignmentRuleType.Fixed).ToList();

        if (fixedRules.Count > 0 && fixedRules.All(rule => rule.DriverId != driver.Id))
        {
            result.IsForbidden = true;
            result.Matches.Add($"Stałe przypisanie: {duty.DutyNumber} ma innego kierowcę");
        }

        foreach (var rule in applicableRules)
        {
            if (rule.DriverId != driver.Id)
            {
                continue;
            }

            var label = BuildMatchLabel(rule, duty);
            if (rule.Type == PlanningAssignmentRuleType.Forbidden)
            {
                result.IsForbidden = true;
                result.Matches.Add(label);
                continue;
            }

            if (rule.Type == PlanningAssignmentRuleType.Fixed)
            {
                result.IsPreferred = true;
                result.PreferenceScore += FixedDutyScore;
                result.Matches.Add(label);
                continue;
            }

            result.IsPreferred = true;
            result.PreferenceScore += PreferredDutyScore;
            result.Matches.Add(label);
        }

        return result;
    }

    public static string NormalizeDutyNumber(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().ToUpperInvariant();

    private static bool MatchesCompany(PlanningAssignmentRule rule, Guid companyId) =>
        rule.CompanyId == companyId;

    private static bool MatchesDate(PlanningAssignmentRule rule, DateOnly date) =>
        (!rule.DateFrom.HasValue || rule.DateFrom.Value <= date)
        && (!rule.DateTo.HasValue || rule.DateTo.Value >= date);

    private static bool MatchesDuty(PlanningAssignmentRule rule, PlanningDuty duty, string dutyNumber)
    {
        if (rule.DutyId.HasValue)
        {
            return rule.DutyId.Value == duty.Id;
        }

        var ruleDutyNumber = NormalizeDutyNumber(rule.DutyNumber);
        return ruleDutyNumber.Length > 0 && ruleDutyNumber == dutyNumber;
    }

    private static string BuildMatchLabel(PlanningAssignmentRule rule, PlanningDuty duty)
    {
        var kind = rule.Type switch
        {
            PlanningAssignmentRuleType.Forbidden => "Zakaz",
            PlanningAssignmentRuleType.Fixed => "Stałe przypisanie",
            _ => "Preferencja"
        };
        var code = !string.IsNullOrWhiteSpace(duty.DutyNumber) ? duty.DutyNumber : duty.Id.ToString();
        return string.IsNullOrWhiteSpace(rule.Note)
            ? $"{kind}: {code}"
            : $"{kind}: {code} ({rule.Note})";
    }
}
