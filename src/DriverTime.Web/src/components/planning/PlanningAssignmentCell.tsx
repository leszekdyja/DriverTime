import type { PlanningGridCell } from "./buildPlanningMonthlyGrid";

type PlanningAssignmentCellProps = {
    cell: PlanningGridCell;
    onClick: () => void;
};

export function PlanningAssignmentCell({ cell, onClick }: PlanningAssignmentCellProps) {
    const title = cell.assignment
        ? [
            cell.assignment.driverFullName,
            cell.assignment.dutyNumber ? `Służba ${cell.assignment.dutyNumber}` : cell.assignment.assignmentType,
            cell.assignment.startDateTime && cell.assignment.endDateTime ? `${cell.assignment.startDateTime} - ${cell.assignment.endDateTime}` : null,
            cell.assignment.notes,
        ].filter(Boolean).join(" · ")
        : "Brak wpisu";

    return (
        <button
            type="button"
            title={title}
            className={`planning-assignment-cell ${cell.kind}${cell.isManual ? " manual" : ""}${cell.isGenerated ? " generated" : ""}`}
            onClick={onClick}
            aria-label={cell.label ? `Edytuj wpis ${cell.label}` : "Dodaj wpis"}
        >
            <span>{cell.label || ""}</span>
            {cell.isManual ? <i aria-hidden="true" /> : null}
        </button>
    );
}
