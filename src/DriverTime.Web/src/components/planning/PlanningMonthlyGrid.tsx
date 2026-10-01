import type { PlanningMonthlyGridModel } from "./buildPlanningMonthlyGrid";
import { PlanningMonthlyGridHeader } from "./PlanningMonthlyGridHeader";
import { PlanningMonthlyGridRow } from "./PlanningMonthlyGridRow";

type PlanningMonthlyGridProps = {
    grid: PlanningMonthlyGridModel;
    onCellClick: (driverId: string, date: string) => void;
};

export function PlanningMonthlyGrid({ grid, onCellClick }: PlanningMonthlyGridProps) {
    return (
        <div className="planning-month-table-wrap" data-testid="planning-monthly-grid">
            <table className="planning-month-table">
                <PlanningMonthlyGridHeader days={grid.days} />
                <tbody>
                    {grid.rows.map((row) => <PlanningMonthlyGridRow key={row.driver.id} row={row} onCellClick={onCellClick} />)}
                </tbody>
            </table>
        </div>
    );
}
