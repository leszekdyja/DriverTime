import { saveAs } from "file-saver";
import type {
    Cell as ExcelCell,
    Row as ExcelRow,
    Worksheet,
} from "exceljs";
import type {
    Cell as WriteExcelCell,
    CellObject,
    SheetData,
    Value,
} from "write-excel-file/browser";

import type { ReportActivity, ReportDriver } from "./reportsService";
import type { DriverViolation } from "./violationsService";

type ReportTotals = {
    driving: number;
    rest: number;
    work: number;
    availability: number;
};

type ReportExcelOptions = {
    activities: ReportActivity[];
    driver?: ReportDriver;
    dateFrom: string;
    dateTo: string;
    totals: ReportTotals;
};

const severityLabels: Record<string, string> = {
    low: "Niski",
    medium: "Średni",
    high: "Wysoki",
    minor: "Minor",
    serious: "Serious",
    "very serious": "Very serious",
};

const activityLabels: Record<string, string> = {
    DRIVING: "Jazda",
    WORK: "Praca",
    REST: "Odpoczynek",
    AVAILABILITY: "Dyspozycja",
};

const activityFills: Record<string, string> = {
    DRIVING: "DBEAFE",
    WORK: "EDE9FE",
    REST: "DCFCE7",
    AVAILABILITY: "FFEDD5",
    OTHER: "F1F5F9",
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

function driverName(firstName: string, lastName: string) {
    return [firstName, lastName].filter(Boolean).join(" ") || "Brak danych";
}

function getDriverName(driver?: ReportDriver) {
    return driver ? driverName(driver.firstName, driver.lastName) : "Wszyscy kierowcy";
}

function getDateRange(dateFrom: string, dateTo: string) {
    return `${dateFrom || "Początek danych"} - ${dateTo || "Koniec danych"}`;
}

function generatedAtRow(): WriteExcelCell[] {
    return [labelCell("Data wygenerowania"), dateCell(new Date().toISOString())];
}

function getActivityLabel(activityType: string) {
    return activityLabels[activityType.toUpperCase()] ?? "Inne";
}

function getActivityFill(activityType: string) {
    return activityFills[activityType.toUpperCase()] ?? activityFills.OTHER;
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return `${hours}:${minutes.toString().padStart(2, "0")}`;
}

function getVehicle(activity: ReportActivity) {
    return activity.vehicleRegistration
        || activity.vehicleRegistrationNumber
        || activity.vehicle
        || "Brak danych";
}

function safeFileName(value: string) {
    return value
        .normalize("NFKD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/[^a-zA-Z0-9-]+/g, "-")
        .replace(/^-+|-+$/g, "")
        .toLowerCase();
}

function applyThinBorder(cell: ExcelCell) {
    cell.border = {
        top: { style: "thin", color: { argb: "E2E8F0" } },
        right: { style: "thin", color: { argb: "E2E8F0" } },
        bottom: { style: "thin", color: { argb: "E2E8F0" } },
        left: { style: "thin", color: { argb: "E2E8F0" } },
    };
}

function applyHeaderStyle(row: ExcelRow) {
    row.eachCell((cell) => {
        cell.font = { bold: true, color: { argb: "FFFFFF" } };
        cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "1D4ED8" } };
        cell.alignment = { vertical: "middle", horizontal: "center", wrapText: true };
        applyThinBorder(cell);
    });
}

function autoFitColumns(worksheet: Worksheet) {
    worksheet.columns.forEach((column) => {
        let width = 12;

        column.eachCell?.({ includeEmpty: true }, (cell) => {
            const value = cell.value;
            const length = value instanceof Date
                ? 18
                : String(value ?? "").length;
            width = Math.max(width, Math.min(length + 3, 46));
        });

        column.width = width;
    });
}

function styleInfoRow(row: ExcelRow) {
    row.eachCell((cell, index) => {
        applyThinBorder(cell);
        cell.alignment = { vertical: "middle", wrapText: true };

        if (index % 2 === 1) {
            cell.font = { bold: true, color: { argb: "334155" } };
            cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "E2E8F0" } };
        }
    });
}

export async function exportReportExcel(options: ReportExcelOptions) {
    const { default: ExcelJS } = await import("exceljs");
    const workbook = new ExcelJS.Workbook();
    workbook.creator = "DriverTime";
    workbook.created = new Date();
    workbook.modified = new Date();
    workbook.properties.date1904 = false;

    const summarySheet = workbook.addWorksheet("Podsumowanie", {
        views: [{ showGridLines: false }],
    });
    const activitySheet = workbook.addWorksheet("Aktywności", {
        views: [{ state: "frozen", ySplit: 10, showGridLines: false }],
    });

    const driver = getDriverName(options.driver);
    const dateRange = getDateRange(options.dateFrom, options.dateTo);

    summarySheet.mergeCells("A1:E1");
    summarySheet.getCell("A1").value = "DriverTime";
    summarySheet.getCell("A1").font = { bold: true, size: 22, color: { argb: "FFFFFF" } };
    summarySheet.getCell("A1").fill = { type: "pattern", pattern: "solid", fgColor: { argb: "0F172A" } };
    summarySheet.getCell("A1").alignment = { vertical: "middle" };
    summarySheet.getRow(1).height = 34;

    summarySheet.mergeCells("A2:E2");
    summarySheet.getCell("A2").value = "Raport aktywności kierowcy";
    summarySheet.getCell("A2").font = { bold: true, size: 15, color: { argb: "0F172A" } };
    summarySheet.getCell("A2").fill = { type: "pattern", pattern: "solid", fgColor: { argb: "EFF6FF" } };
    summarySheet.getRow(2).height = 28;

    [
        ["Kierowca", driver, "Numer karty", options.driver?.cardNumber || "Wszystkie"],
        ["Zakres dat", dateRange, "Wygenerowano", new Date()],
    ].forEach((values) => {
        const row = summarySheet.addRow(values);
        row.height = 24;
        row.getCell(4).numFmt = "dd.mm.yyyy hh:mm";
        styleInfoRow(row);
    });

    summarySheet.addRow([]);
    const summaryHeader = summarySheet.addRow(["Jazda", "Praca", "Odpoczynek", "Dyspozycja", "Liczba aktywności"]);
    applyHeaderStyle(summaryHeader);
    const summaryValues = summarySheet.addRow([
        formatDuration(options.totals.driving),
        formatDuration(options.totals.work),
        formatDuration(options.totals.rest),
        formatDuration(options.totals.availability),
        options.activities.length,
    ]);
    summaryValues.eachCell((cell) => {
        cell.font = { bold: true, size: 13, color: { argb: "0F172A" } };
        cell.alignment = { horizontal: "center", vertical: "middle" };
        applyThinBorder(cell);
    });
    autoFitColumns(summarySheet);

    activitySheet.mergeCells("A1:E1");
    activitySheet.getCell("A1").value = "DriverTime";
    activitySheet.getCell("A1").font = { bold: true, size: 22, color: { argb: "FFFFFF" } };
    activitySheet.getCell("A1").fill = { type: "pattern", pattern: "solid", fgColor: { argb: "0F172A" } };
    activitySheet.getRow(1).height = 34;

    activitySheet.mergeCells("A2:E2");
    activitySheet.getCell("A2").value = "Tabela aktywności";
    activitySheet.getCell("A2").font = { bold: true, size: 15, color: { argb: "0F172A" } };
    activitySheet.getCell("A2").fill = { type: "pattern", pattern: "solid", fgColor: { argb: "F8FAFC" } };

    [
        ["Kierowca", driver, "Numer karty", options.driver?.cardNumber || "Wszystkie"],
        ["Zakres dat", dateRange, "Wygenerowano", new Date()],
    ].forEach((values) => {
        const row = activitySheet.addRow(values);
        row.getCell(4).numFmt = "dd.mm.yyyy hh:mm";
        styleInfoRow(row);
    });

    activitySheet.addRow([]);
    activitySheet.addRow(["Podsumowanie", "Jazda", "Praca", "Odpoczynek", "Dyspozycja"]);
    applyHeaderStyle(activitySheet.getRow(7));
    const topSummary = activitySheet.addRow([
        "Czas",
        formatDuration(options.totals.driving),
        formatDuration(options.totals.work),
        formatDuration(options.totals.rest),
        formatDuration(options.totals.availability),
    ]);
    topSummary.eachCell((cell) => {
        cell.font = { bold: true, color: { argb: "0F172A" } };
        cell.alignment = { horizontal: "center" };
        applyThinBorder(cell);
    });
    activitySheet.addRow([]);

    const tableHeader = activitySheet.addRow(["Start", "Koniec", "Aktywność", "Czas", "Pojazd"]);
    tableHeader.height = 26;
    applyHeaderStyle(tableHeader);

    options.activities.forEach((activity) => {
        const row = activitySheet.addRow([
            new Date(activity.startUtc),
            new Date(activity.endUtc),
            getActivityLabel(activity.activityType),
            formatDuration(activity.durationSeconds),
            getVehicle(activity),
        ]);
        const fill = getActivityFill(activity.activityType);

        row.eachCell((cell, index) => {
            cell.alignment = { vertical: "middle", wrapText: true };
            applyThinBorder(cell);

            if (index === 1 || index === 2) {
                cell.numFmt = "dd.mm.yyyy hh:mm";
            }

            if (index === 3) {
                cell.font = { bold: true, color: { argb: "0F172A" } };
                cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: fill } };
            }
        });
    });

    const headerRowNumber = 10;
    activitySheet.autoFilter = {
        from: { row: headerRowNumber, column: 1 },
        to: { row: headerRowNumber, column: 5 },
    };
    autoFitColumns(activitySheet);

    const buffer = await workbook.xlsx.writeBuffer();
    const selectedDriver = safeFileName(driver || "wszyscy-kierowcy");
    const datePart = safeFileName(`${options.dateFrom || "start"}-${options.dateTo || "koniec"}`);
    const fileName = `drivertime-raport-${selectedDriver}-${datePart}.xlsx`;

    saveAs(
        new Blob([buffer], {
            type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;charset=utf-8",
        }),
        fileName,
    );
}

export async function exportViolationsExcel(violations: DriverViolation[]) {
    const { default: writeExcelFile } = await import("write-excel-file/browser");
    const sheet: SheetData = [
        [titleCell],
        [{ value: "Raport naruszeń czasu pracy", fontSize: 14, fontWeight: "bold", columnSpan: 6 }],
        generatedAtRow(),
        [labelCell("Liczba naruszeń"), violations.length],
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
            dataCell(severityLabels[violation.severity.toLowerCase()] ?? violation.severity),
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
