import type { Cell, CellObject, SheetData, Value } from "write-excel-file/browser";

import type { ReportActivity, ReportDriver } from "./reportsService";
import type { DriverViolation } from "./violationsService";

type ReportTotals = {
    driving: number;
    rest: number;
    work: number;
};

type ReportExcelOptions = {
    activities: ReportActivity[];
    driver?: ReportDriver;
    dateFrom: string;
    dateTo: string;
    totals: ReportTotals;
};

const severityLabels: Record<DriverViolation["severity"], string> = {
    low: "Niski",
    medium: "Sredni",
    high: "Wysoki",
};

const titleCell: CellObject = {
    value: "DriverTime",
    fontSize: 18,
    fontWeight: "bold",
    textColor: "#FFFFFF",
    backgroundColor: "#0F172A",
    align: "left",
    alignVertical: "center",
    height: 32,
    columnSpan: 6,
};

function headerCell(value: string): CellObject {
    return {
        value,
        fontWeight: "bold",
        textColor: "#FFFFFF",
        backgroundColor: "#2563EB",
        borderColor: "#CBD5E1",
        borderStyle: "thin",
        alignVertical: "center",
        height: 24,
        wrap: true,
    };
}

function labelCell(value: string): CellObject {
    return {
        value,
        fontWeight: "bold",
        backgroundColor: "#E2E8F0",
    };
}

function dataCell(value: Value | null | undefined): CellObject {
    return {
        value: value ?? "",
        borderColor: "#E2E8F0",
        borderStyle: "thin",
        alignVertical: "top",
        wrap: true,
    };
}

function dateCell(value: string): CellObject {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? dataCell("Brak danych")
        : {
            ...dataCell(date),
            type: Date,
            format: "dd.mm.yyyy hh:mm",
        };
}

function durationCell(seconds: number): CellObject {
    return {
        ...dataCell(Math.max(seconds, 0) / 86400),
        type: Number,
        format: "[h]:mm",
        align: "right",
    };
}

function driverName(firstName: string, lastName: string) {
    return [firstName, lastName].filter(Boolean).join(" ") || "Brak danych";
}

function generatedAtRow(): Cell[] {
    return [labelCell("Data wygenerowania"), dateCell(new Date().toISOString())];
}

export async function exportReportExcel(options: ReportExcelOptions) {
    const { default: writeExcelFile } = await import("write-excel-file/browser");
    const selectedDriver = options.driver
        ? driverName(options.driver.firstName, options.driver.lastName)
        : "Wszyscy kierowcy";
    const sheet: SheetData = [
        [titleCell],
        [{ value: "Raport aktywnosci kierowcow", fontSize: 14, fontWeight: "bold", columnSpan: 6 }],
        generatedAtRow(),
        [labelCell("Kierowca"), selectedDriver, labelCell("Numer karty"), options.driver?.cardNumber || "Wszystkie"],
        [labelCell("Zakres dat"), `${options.dateFrom || "Poczatek danych"} - ${options.dateTo || "Koniec danych"}`],
        [],
        [headerCell("Czas jazdy"), headerCell("Czas pracy"), headerCell("Czas odpoczynku"), headerCell("Liczba aktywnosci")],
        [durationCell(options.totals.driving), durationCell(options.totals.work), durationCell(options.totals.rest), dataCell(options.activities.length)],
        [],
        [
            headerCell("Kierowca"),
            headerCell("Numer karty"),
            headerCell("Poczatek"),
            headerCell("Koniec"),
            headerCell("Aktywnosc"),
            headerCell("Czas"),
        ],
        ...options.activities.map((activity) => [
            dataCell(driverName(activity.driverFirstName, activity.driverLastName)),
            dataCell(activity.driverCardNumber || "Brak danych"),
            dateCell(activity.startUtc),
            dateCell(activity.endUtc),
            dataCell(activity.activityType || "Brak danych"),
            durationCell(activity.durationSeconds),
        ]),
    ];

    await writeExcelFile(sheet, {
        sheet: "Raport aktywnosci",
        columns: [
            { width: 26 },
            { width: 24 },
            { width: 20 },
            { width: 20 },
            { width: 18 },
            { width: 14 },
        ],
        stickyRowsCount: 10,
    }).toFile("drivertime-activities-report.xlsx");
}

export async function exportViolationsExcel(violations: DriverViolation[]) {
    const { default: writeExcelFile } = await import("write-excel-file/browser");
    const sheet: SheetData = [
        [titleCell],
        [{ value: "Raport naruszen czasu pracy", fontSize: 14, fontWeight: "bold", columnSpan: 6 }],
        generatedAtRow(),
        [labelCell("Liczba naruszen"), violations.length],
        [],
        [
            headerCell("Kierowca"),
            headerCell("Numer karty"),
            headerCell("Typ naruszenia"),
            headerCell("Data"),
            headerCell("Opis"),
            headerCell("Poziom"),
        ],
        ...violations.map((violation) => [
            dataCell(driverName(violation.driverFirstName, violation.driverLastName)),
            dataCell(violation.driverCardNumber || "Brak danych"),
            dataCell(violation.violationType),
            dateCell(violation.occurredAtUtc),
            dataCell(violation.description),
            dataCell(severityLabels[violation.severity]),
        ]),
    ];

    await writeExcelFile(sheet, {
        sheet: "Naruszenia",
        columns: [
            { width: 25 },
            { width: 22 },
            { width: 30 },
            { width: 20 },
            { width: 55 },
            { width: 14 },
        ],
        stickyRowsCount: 6,
    }).toFile("drivertime-violations-report.xlsx");
}
