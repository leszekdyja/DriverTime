import type { jsPDF as PdfDocument } from "jspdf";

import type { ReportActivity, ReportDriver } from "./reportsService";
import type { DriverViolation } from "./violationsService";
import { getComplianceRuleLabel, getSeverityLabel } from "../utils/complianceLabels";
import { formatDriverNameOrFallback } from "../utils/driverName";

type ReportTotals = {
    driving: number;
    rest: number;
    work: number;
};

type ReportPdfOptions = {
    activities: ReportActivity[];
    driver?: ReportDriver;
    dateFrom: string;
    dateTo: string;
    totals: ReportTotals;
};

const PDF_FONT_NAME = "NotoSans";

const severityLabels: Record<string, string> = {
    low: "Niski",
    medium: "Średni",
    high: "Wysoki",
    minor: "Niskie",
    serious: "Ostrzeżenie",
    "very serious": "Krytyczne",
};

async function loadFontAsBase64(path: string) {
    const response = await fetch(path);

    if (!response.ok) {
        throw new Error(`Nie udało się załadować czcionki PDF: ${path}`);
    }

    const buffer = await response.arrayBuffer();
    let binary = "";

    const bytes = new Uint8Array(buffer);
    bytes.forEach((byte) => {
        binary += String.fromCharCode(byte);
    });

    return window.btoa(binary);
}

async function registerPdfFonts(document: PdfDocument) {
    const [regularFont, boldFont] = await Promise.all([
        loadFontAsBase64("/fonts/NotoSans-Regular.ttf"),
        loadFontAsBase64("/fonts/NotoSans-Bold.ttf"),
    ]);

    document.addFileToVFS("NotoSans-Regular.ttf", regularFont);
    document.addFont("NotoSans-Regular.ttf", PDF_FONT_NAME, "normal");

    document.addFileToVFS("NotoSans-Bold.ttf", boldFont);
    document.addFont("NotoSans-Bold.ttf", PDF_FONT_NAME, "bold");

    document.setFont(PDF_FONT_NAME, "normal");
}

function formatDate(value: string) {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return "Brak danych";
    }

    return new Intl.DateTimeFormat("pl-PL", {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
    }).format(date);
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return `${hours} godz. ${minutes} min`;
}

function getDriverName(firstName: string, lastName: string) {
    return formatDriverNameOrFallback(firstName, lastName);
}

function addHeader(document: PdfDocument, title: string, subtitle: string) {
    document.setFillColor(15, 23, 42);
    document.rect(0, 0, 297, 30, "F");

    document.setTextColor(255, 255, 255);
    document.setFontSize(18);
    document.setFont(PDF_FONT_NAME, "bold");
    document.text("DriverTime", 14, 13);

    document.setFontSize(11);
    document.setFont(PDF_FONT_NAME, "normal");
    document.text(title, 14, 22);

    document.setFontSize(9);
    document.text(subtitle, 283, 13, { align: "right" });
    document.text(`Wygenerowano: ${formatDate(new Date().toISOString())}`, 283, 22, {
        align: "right",
    });

    document.setTextColor(15, 23, 42);
}

export async function exportReportPdf(options: ReportPdfOptions) {
    const [{ jsPDF }, { default: autoTable }] = await Promise.all([
        import("jspdf"),
        import("jspdf-autotable"),
    ]);

    const document = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });
    await registerPdfFonts(document);

    const driverName = options.driver
        ? getDriverName(options.driver.firstName, options.driver.lastName)
        : "Wszyscy kierowcy";

    const dateRange = `${options.dateFrom || "początek danych"} - ${options.dateTo || "koniec danych"}`;

    addHeader(document, "Raport aktywności kierowców", `Zakres: ${dateRange}`);

    document.setFontSize(10);
    document.setFont(PDF_FONT_NAME, "bold");
    document.text("Kierowca", 14, 41);
    document.text("Numer karty", 95, 41);
    document.text("Zakres dat", 176, 41);

    document.setFont(PDF_FONT_NAME, "normal");
    document.text(driverName, 14, 47);
    document.text(options.driver?.cardNumber || "Wszystkie numery kart", 95, 47);
    document.text(dateRange, 176, 47);

    autoTable(document, {
        startY: 55,
        theme: "grid",
        head: [["Czas jazdy", "Czas pracy", "Czas odpoczynku", "Liczba aktywności"]],
        body: [[
            formatDuration(options.totals.driving),
            formatDuration(options.totals.work),
            formatDuration(options.totals.rest),
            String(options.activities.length),
        ]],
        styles: {
            font: PDF_FONT_NAME,
            fontSize: 9,
            cellPadding: 3,
        },
        headStyles: {
            font: PDF_FONT_NAME,
            fontStyle: "bold",
            fillColor: [37, 99, 235],
            textColor: 255,
        },
    });

    autoTable(document, {
        startY: 78,
        theme: "striped",
        head: [["Kierowca", "Numer karty", "Początek", "Koniec", "Aktywność", "Czas"]],
        body: options.activities.map((activity) => [
            getDriverName(activity.driverFirstName, activity.driverLastName),
            activity.driverCardNumber || "Brak danych",
            formatDate(activity.startUtc),
            formatDate(activity.endUtc),
            activity.activityType || "Brak danych",
            formatDuration(activity.durationSeconds),
        ]),
        styles: {
            font: PDF_FONT_NAME,
            fontSize: 8,
            cellPadding: 2.5,
            overflow: "linebreak",
        },
        headStyles: {
            font: PDF_FONT_NAME,
            fontStyle: "bold",
            fillColor: [15, 23, 42],
            textColor: 255,
        },
        alternateRowStyles: { fillColor: [241, 245, 249] },
        margin: { left: 14, right: 14 },
    });

    document.save("drivertime-raport-aktywnosci.pdf");
}

export async function exportViolationsPdf(violations: DriverViolation[]) {
    const [{ jsPDF }, { default: autoTable }] = await Promise.all([
        import("jspdf"),
        import("jspdf-autotable"),
    ]);

    const document = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });
    await registerPdfFonts(document);

    addHeader(document, "Raport naruszeń czasu pracy", `${violations.length} naruszeń`);

    autoTable(document, {
        startY: 38,
        theme: "striped",
        head: [["Kierowca", "Numer karty", "Typ naruszenia", "Data", "Opis", "Poziom"]],
        body: violations.map((violation) => [
            getDriverName(violation.driverFirstName, violation.driverLastName),
            violation.driverCardNumber || "Brak danych",
            getComplianceRuleLabel(violation.violationType, violation.code),
            formatDate(violation.occurredAtUtc),
            violation.description,
            getSeverityLabel(severityLabels[violation.severity.toLowerCase()] ?? violation.severity),
        ]),
        columnStyles: {
            0: { cellWidth: 38 },
            1: { cellWidth: 35 },
            2: { cellWidth: 45 },
            3: { cellWidth: 35 },
            4: { cellWidth: "auto" },
            5: { cellWidth: 22 },
        },
        styles: {
            font: PDF_FONT_NAME,
            fontSize: 8,
            cellPadding: 3,
            overflow: "linebreak",
        },
        headStyles: {
            font: PDF_FONT_NAME,
            fontStyle: "bold",
            fillColor: [15, 23, 42],
            textColor: 255,
        },
        alternateRowStyles: { fillColor: [241, 245, 249] },
        margin: { left: 14, right: 14 },
    });

    document.save("drivertime-raport-naruszen.pdf");
}

type ComplianceReportDriver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
};

type ComplianceReportActivity = {
    activityType: string;
    startUtc: string;
    endUtc: string;
    durationSeconds: number;
};

type ComplianceReportPdfOptions = {
    driver: ComplianceReportDriver;
    activities: ComplianceReportActivity[];
    violations: DriverViolation[];
};

export async function exportComplianceReportPdf(options: ComplianceReportPdfOptions) {
    const [{ jsPDF }, { default: autoTable }] = await Promise.all([
        import("jspdf"),
        import("jspdf-autotable"),
    ]);

    const document = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });
    await registerPdfFonts(document);

    const driverName = getDriverName(options.driver.firstName, options.driver.lastName);
    const activityRange = getComplianceActivityRange(options.activities);
    const severitySummary = getComplianceSeveritySummary(options.violations);
    const activitySummary = getComplianceActivitySummary(options.activities);

    addHeader(document, "Raport zgodności kierowcy", `Kierowca: ${driverName}`);

    document.setFontSize(10);
    document.setFont(PDF_FONT_NAME, "bold");
    document.text("Kierowca", 14, 41);
    document.text("Numer karty", 95, 41);
    document.text("Zakres analizowanych danych", 176, 41);

    document.setFont(PDF_FONT_NAME, "normal");
    document.text(driverName, 14, 47);
    document.text(options.driver.cardNumber || "Brak danych", 95, 47);
    document.text(activityRange, 176, 47);

    autoTable(document, {
        startY: 55,
        theme: "grid",
        head: [["Naruszenia", "High", "Medium", "Low", "Jazda", "Praca", "Dyspozycja", "Odpoczynek", "Suma"]],
        body: [[
            String(options.violations.length),
            String(severitySummary.high),
            String(severitySummary.medium),
            String(severitySummary.low),
            formatDuration(activitySummary.driving),
            formatDuration(activitySummary.work),
            formatDuration(activitySummary.availability),
            formatDuration(activitySummary.rest),
            formatDuration(activitySummary.total),
        ]],
        styles: {
            font: PDF_FONT_NAME,
            fontSize: 8,
            cellPadding: 2.5,
        },
        headStyles: {
            font: PDF_FONT_NAME,
            fontStyle: "bold",
            fillColor: [37, 99, 235],
            textColor: 255,
        },
        margin: { left: 14, right: 14 },
    });

    autoTable(document, {
        startY: 78,
        theme: "striped",
        head: [["Kod", "Reguła", "Severity", "Opis", "Okres"]],
        body: options.violations.map((violation) => [
            violation.code || "Brak kodu",
            getComplianceRuleLabel(violation.violationType, violation.code),
            getSeverityLabel(severityLabels[violation.severity.toLowerCase()] ?? violation.severity),
            violation.description || "Brak opisu",
            `${formatDate(violation.occurredAtUtc)} - ${formatDate(violation.periodEndUtc)}`,
        ]),
        columnStyles: {
            0: { cellWidth: 34 },
            1: { cellWidth: 52 },
            2: { cellWidth: 24 },
            3: { cellWidth: "auto" },
            4: { cellWidth: 56 },
        },
        styles: {
            font: PDF_FONT_NAME,
            fontSize: 7.5,
            cellPadding: 2.4,
            overflow: "linebreak",
        },
        headStyles: {
            font: PDF_FONT_NAME,
            fontStyle: "bold",
            fillColor: [15, 23, 42],
            textColor: 255,
        },
        alternateRowStyles: { fillColor: [241, 245, 249] },
        margin: { left: 14, right: 14 },
    });

    document.save(`drivertime-raport-zgodnosci-${options.driver.cardNumber || options.driver.id}.pdf`);
}

function getComplianceActivityRange(activities: ComplianceReportActivity[]) {
    if (activities.length === 0) {
        return "Brak aktywności";
    }

    const starts = activities
        .map((activity) => new Date(activity.startUtc).getTime())
        .filter((value) => !Number.isNaN(value));
    const ends = activities
        .map((activity) => new Date(activity.endUtc).getTime())
        .filter((value) => !Number.isNaN(value));

    if (starts.length === 0 || ends.length === 0) {
        return "Brak danych";
    }

    return `${formatDate(new Date(Math.min(...starts)).toISOString())} - ${formatDate(new Date(Math.max(...ends)).toISOString())}`;
}

function getComplianceSeveritySummary(violations: DriverViolation[]) {
    return violations.reduce(
        (summary, violation) => {
            const severity = violation.severity.trim().toLowerCase();

            if (severity === "critical" || severity === "high" || severity === "severe" || severity === "very serious") {
                summary.high += 1;
            } else if (severity === "warning" || severity === "medium" || severity === "serious") {
                summary.medium += 1;
            } else {
                summary.low += 1;
            }

            return summary;
        },
        { high: 0, medium: 0, low: 0 },
    );
}

function getComplianceActivitySummary(activities: ComplianceReportActivity[]) {
    return activities.reduce(
        (summary, activity) => {
            const duration = Math.max(activity.durationSeconds, 0);
            const type = activity.activityType.toUpperCase();

            if (type === "DRIVING") {
                summary.driving += duration;
            } else if (type === "WORK") {
                summary.work += duration;
            } else if (type === "AVAILABILITY") {
                summary.availability += duration;
            } else if (type === "REST") {
                summary.rest += duration;
            }

            summary.total += duration;

            return summary;
        },
        { driving: 0, work: 0, availability: 0, rest: 0, total: 0 },
    );
}
