import type { PlanningGridRow } from "./buildPlanningMonthlyGrid";
import { PlanningAssignmentCell } from "./PlanningAssignmentCell";

type PlanningMonthlyGridRowProps = {
    row: PlanningGridRow;
    onCellClick: (driverId: string, date: string) => void;
};

export function PlanningMonthlyGridRow({ row, onCellClick }: PlanningMonthlyGridRowProps) {
    return (
        <tr>
            <th className="planning-grid-driver-name"><span>{row.driverName}</span>{row.driver.includeInPlanning === false ? <small className="planning-driver-auto-off">Auto wył.</small> : null}</th>
            {row.cells.map((cell) => (
                <td key={cell.date}>
                    <PlanningAssignmentCell cell={cell} onClick={() => onCellClick(row.driver.id, cell.date)} />
                </td>
            ))}
        </tr>
    );
}

