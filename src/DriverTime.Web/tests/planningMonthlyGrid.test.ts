import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
    buildPlanningMonthDays,
    buildPlanningMonthlyGrid,
    getPlanningAssignmentCellKind,
    getPlanningAssignmentCellLabel,
    type PlanningGridAssignment,
    type PlanningGridDriver,
} from "../src/components/planning/buildPlanningMonthlyGrid";
import { getPlanningDutyMask, planningWeekdaysMask, planningWeekendMask, togglePlanningDutyDay } from "../src/components/planning/planningDutyDayConfig";

function driver(id: string, firstName: string, lastName: string, includeInPlanning = true): PlanningGridDriver {
    return { id, firstName, lastName, cardNumber: id, includeInPlanning };
}

function assignment(partial: Partial<PlanningGridAssignment>): PlanningGridAssignment {
    return {
        id: partial.id ?? crypto.randomUUID(),
        date: partial.date ?? "2026-08-01",
        driverId: partial.driverId ?? "d1",
        driverFullName: partial.driverFullName ?? "Kowalski Jan",
        planningDutyId: partial.planningDutyId ?? "duty-1",
        dutyNumber: partial.dutyNumber ?? "101",
        line: partial.line ?? null,
        startDateTime: partial.startDateTime ?? "2026-08-01T08:00:00",
        endDateTime: partial.endDateTime ?? "2026-08-01T16:00:00",
        status: partial.status ?? "Generated",
        assignmentType: partial.assignmentType ?? "Duty",
        notes: partial.notes ?? null,
    };
}

assert.equal(getPlanningDutyMask(null), planningWeekdaysMask);
assert.equal(getPlanningDutyMask(undefined), planningWeekdaysMask);
assert.equal(getPlanningDutyMask(0), 0);
assert.equal(togglePlanningDutyDay(0, 32, true), 32);
assert.equal(togglePlanningDutyDay(planningWeekendMask, 32, false), 64);
assert.equal(buildPlanningMonthDays(2026, 2).length, 28);
assert.equal(buildPlanningMonthDays(2024, 2).length, 29);
assert.equal(buildPlanningMonthDays(2026, 4).length, 30);
assert.equal(buildPlanningMonthDays(2026, 8).length, 31);

const drivers = [driver("b", "Marek", "Kowalski", false), driver("a", "Jan", "Adamczewski")];
const assignments = [
    assignment({ id: "a1", driverId: "a", date: "2026-08-02", dutyNumber: "RN", startDateTime: "2026-08-02T20:00:00", endDateTime: "2026-08-03T06:00:00" }),
    assignment({ id: "b1", driverId: "b", date: "2026-08-03", dutyNumber: "R", status: "Manual" }),
    assignment({ id: "b2", driverId: "b", date: "2026-08-04", dutyNumber: "R2" }),
    assignment({ id: "b3", driverId: "b", date: "2026-08-05", dutyNumber: "WG" }),
    assignment({ id: "b4", driverId: "b", date: "2026-08-06", dutyNumber: "W" }),
    assignment({ id: "b5", driverId: "b", date: "2026-08-07", dutyNumber: null, planningDutyId: null, assignmentType: "Vacation" }),
    assignment({ id: "b6", driverId: "b", date: "2026-08-08", dutyNumber: null, planningDutyId: null, assignmentType: "SickLeave" }),
    assignment({ id: "b7", driverId: "b", date: "2026-08-09", dutyNumber: null, planningDutyId: null, assignmentType: "Other", notes: "Niedostępność" }),
];
const grid = buildPlanningMonthlyGrid(drivers, assignments, 2026, 8, ["2026-08-15"]);

assert.equal(grid.rows[0].driverName, "Adamczewski Jan");
assert.equal(grid.rows[1].driverName, "Kowalski Marek");
assert.equal(grid.rows[1].driver.includeInPlanning, false);
assert.equal(grid.rows[0].cells[1].assignment?.id, "a1");
assert.equal(grid.rows[0].cells[1].label, "RN");
assert.equal(grid.rows[0].cells[1].kind, "night");
assert.equal(grid.rows[1].cells[2].kind, "reserve");
assert.equal(grid.rows[1].cells[3].label, "R2");
assert.equal(grid.rows[1].cells[4].kind, "dayOff");
assert.equal(grid.rows[1].cells[5].label, "W");
assert.equal(grid.rows[1].cells[6].label, "UW");
assert.equal(grid.rows[1].cells[7].label, "CH");
assert.equal(grid.rows[1].cells[8].kind, "unavailable");
assert.equal(grid.rows[1].cells[8].label, "ND");
assert.equal(grid.rows[1].cells[2].isManual, true);
assert.equal(grid.rows[0].cells[1].isGenerated, true);
assert.equal(grid.rows[0].cells[0].kind, "empty");
assert.equal(grid.rows[0].cells[1].date, "2026-08-02");
assert.equal(grid.days.some((day) => day.isSaturday), true);
assert.equal(grid.days.some((day) => day.isSunday), true);
assert.equal(grid.days.find((day) => day.date === "2026-08-15")?.isHoliday, true);
assert.equal(getPlanningAssignmentCellLabel(assignment({ dutyNumber: "110" })), "110");
assert.equal(getPlanningAssignmentCellKind(assignment({ dutyNumber: "RN" })), "night");

const planningSchedulesSource = readFileSync(resolve("src/components/planning/PlanningSchedulesTab.tsx"), "utf8");
assert.equal(planningSchedulesSource.includes("Kierowcy uwzględniani w planowaniu"), false);
assert.equal(planningSchedulesSource.includes("Do planowania wybrano"), true);
assert.equal(planningSchedulesSource.includes("/drivers"), true);
const driversPageSource = readFileSync(resolve("src/pages/DriversPage.tsx"), "utf8");
assert.equal(driversPageSource.includes("<th>Planowanie</th>"), true);
assert.equal(driversPageSource.includes("toggleDriverPlanning"), true);

console.log("planningMonthlyGrid tests passed");





