export type PlanningGridDriver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    includeInPlanning?: boolean;
};

export type PlanningGridAssignment = {
    id: string;
    date: string;
    driverId: string;
    driverFullName: string;
    planningDutyId: string | null;
    dutyNumber: string | null;
    line: string | null;
    startDateTime: string | null;
    endDateTime: string | null;
    status: string;
    assignmentType: string;
    notes: string | null;
};

export type PlanningMonthDay = {
    date: string;
    day: number;
    weekday: string;
    isSaturday: boolean;
    isSunday: boolean;
    isHoliday: boolean;
};

export type PlanningGridCell = {
    date: string;
    assignment: PlanningGridAssignment | null;
    label: string;
    kind: "empty" | "duty" | "night" | "reserve" | "dayOff" | "vacation" | "sickLeave" | "unavailable" | "special";
    isManual: boolean;
    isGenerated: boolean;
};

export type PlanningGridRow = {
    driver: PlanningGridDriver;
    driverName: string;
    cells: PlanningGridCell[];
};

export type PlanningMonthlyGridModel = {
    year: number;
    month: number;
    days: PlanningMonthDay[];
    rows: PlanningGridRow[];
};

const weekdayLabels = ["nd", "pon", "wt", "śr", "czw", "pt", "sob"];

export function formatDriverLastFirst(driver: PlanningGridDriver): string {
    const name = `${driver.lastName ?? ""} ${driver.firstName ?? ""}`.trim();
    return name || driver.cardNumber || "Kierowca";
}

export function toPlanningDateKey(year: number, month: number, day: number): string {
    return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

export function buildPlanningMonthDays(year: number, month: number, holidayDates: string[] = []): PlanningMonthDay[] {
    const holidaySet = new Set(holidayDates);
    const daysInMonth = new Date(year, month, 0).getDate();

    return Array.from({ length: daysInMonth }, (_, index) => {
        const day = index + 1;
        const date = toPlanningDateKey(year, month, day);
        const jsDate = new Date(year, month - 1, day);
        const weekdayIndex = jsDate.getDay();
        return {
            date,
            day,
            weekday: weekdayLabels[weekdayIndex],
            isSaturday: weekdayIndex === 6,
            isSunday: weekdayIndex === 0,
            isHoliday: holidaySet.has(date),
        };
    });
}

export function buildPlanningMonthlyGrid(
    drivers: PlanningGridDriver[],
    assignments: PlanningGridAssignment[],
    year: number,
    month: number,
    holidayDates: string[] = []): PlanningMonthlyGridModel {
    const days = buildPlanningMonthDays(year, month, holidayDates);
    const assignmentsByDriverAndDate = new Map<string, PlanningGridAssignment>();
    for (const assignment of assignments) {
        assignmentsByDriverAndDate.set(`${assignment.driverId}|${assignment.date}`, assignment);
    }

    const sortedDrivers = [...drivers].sort((left, right) => {
        const byLastName = (left.lastName ?? "").localeCompare(right.lastName ?? "", "pl");
        if (byLastName !== 0) return byLastName;
        const byFirstName = (left.firstName ?? "").localeCompare(right.firstName ?? "", "pl");
        if (byFirstName !== 0) return byFirstName;
        return (left.cardNumber ?? "").localeCompare(right.cardNumber ?? "", "pl");
    });

    return {
        year,
        month,
        days,
        rows: sortedDrivers.map((driver) => ({
            driver,
            driverName: formatDriverLastFirst(driver),
            cells: days.map((day) => {
                const assignment = assignmentsByDriverAndDate.get(`${driver.id}|${day.date}`) ?? null;
                return buildPlanningGridCell(day.date, assignment);
            }),
        })),
    };
}

export function buildPlanningGridCell(date: string, assignment: PlanningGridAssignment | null): PlanningGridCell {
    if (!assignment) {
        return {
            date,
            assignment: null,
            label: "",
            kind: "empty",
            isManual: false,
            isGenerated: false,
        };
    }

    return {
        date,
        assignment,
        label: getPlanningAssignmentCellLabel(assignment),
        kind: getPlanningAssignmentCellKind(assignment),
        isManual: assignment.status === "Manual",
        isGenerated: assignment.status === "Generated",
    };
}

export function getPlanningAssignmentCellLabel(assignment: PlanningGridAssignment): string {
    if (assignment.assignmentType === "Vacation") return "UW";
    if (assignment.assignmentType === "SickLeave") return "CH";
    if (assignment.assignmentType === "DayOff") return "W";
    if (assignment.assignmentType === "Training") return "SZ";
    if (assignment.assignmentType === "Other" && assignment.notes?.toLowerCase().includes("niedost")) return "ND";
    if (assignment.assignmentType !== "Duty") return assignment.notes?.trim() ? "IN" : assignment.assignmentType;
    return assignment.dutyNumber || "S";
}

export function getPlanningAssignmentCellKind(assignment: PlanningGridAssignment): PlanningGridCell["kind"] {
    const code = (assignment.dutyNumber ?? "").trim().toUpperCase();
    if (assignment.assignmentType === "Vacation" || ["UW", "UO", "UB"].includes(code)) return "vacation";
    if (assignment.assignmentType === "SickLeave" || code === "CH") return "sickLeave";
    if (assignment.assignmentType === "Other" && assignment.notes?.toLowerCase().includes("niedost")) return "unavailable";
    if (code === "RN") return "night";
    if (code === "R" || code === "R2") return "reserve";
    if (code === "WG" || code === "W" || assignment.assignmentType === "DayOff") return "dayOff";
    if (assignment.assignmentType === "Duty") return "duty";
    return "special";
}




