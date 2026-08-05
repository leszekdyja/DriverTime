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
        <>
            {result.isPreview ? <p className="drivers-message">Podgląd — poniższe przypisania nie zostały zapisane.</p> : null}
            <div className="planning-generation-summary">
                {items.map(([label, value]) => (
                    <span key={label}>
                        <small>{label}</small>
                        <strong>{value}</strong>
                    </span>
                ))}
            </div>
            {result.isPreview ? (
                <div className="drivers-table-wrapper">
                    <table className="drivers-table planning-table">
                        <thead><tr><th>Data</th><th>Kierowca</th><th>Nr służby</th><th>Start</th><th>Koniec</th></tr></thead>
                        <tbody>
                            {result.proposedAssignments.length === 0 ? (
                                <tr><td colSpan={5}>Brak proponowanych przypisań.</td></tr>
                            ) : result.proposedAssignments.map((assignment) => (
                                <tr key={assignment.id}>
                                    <td>{assignment.workDate}</td>
                                    <td>{assignment.driverFullName}</td>
                                    <td>{assignment.dutyNumber ?? "-"}</td>
                                    <td>{assignment.startDateTime ? new Date(assignment.startDateTime).toLocaleString("pl-PL") : "-"}</td>
                                    <td>{assignment.endDateTime ? new Date(assignment.endDateTime).toLocaleString("pl-PL") : "-"}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            ) : null}
        </>
    );
}
