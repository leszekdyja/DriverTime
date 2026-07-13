import type { PlanningAutoGenerateResult } from "../../services/planningSchedulesService";

type PlanningGenerationSummaryProps = {
    result: PlanningAutoGenerateResult | null;
};

export function PlanningGenerationSummary({ result }: PlanningGenerationSummaryProps) {
    if (!result) return null;

    const weeklyRestWarnings = result.driverSummaries.reduce((sum, driver) => sum + driver.weeklyRestWarnings.length + driver.insufficientWeeklyRestCount, 0);

    const items = [
        ["Wygenerowane", result.generatedCount],
        ["Nieobsadzone służby", result.unassignedCount],
        ["Odrzucone kandydatury", result.candidateRejectionCount],
        ["Konflikty czasowe", result.timeConflictRejectionCount],
        ["Odpoczynek dobowy", result.dailyRestRejectionCount],
        ["Odpoczynek tygodniowy", result.weeklyRestRejectionCount],
        ["Zakazy", result.forbiddenCandidateRejectionCount],
        ["Ostrzeżenia tygodniowe", weeklyRestWarnings],
        ["RN", result.nightDutyGeneratedCount],
        ["Rezerwy", result.reserveGeneratedCount],
        ["WG/W", result.dayOffGeneratedCount],
    ] as const;

    return (
        <div className="planning-generation-summary">
            {items.map(([label, value]) => (
                <span key={label}>
                    <small>{label}</small>
                    <strong>{value}</strong>
                </span>
            ))}
        </div>
    );
}
