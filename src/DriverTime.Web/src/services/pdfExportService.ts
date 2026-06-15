import type { jsPDF as PdfDocument } from "jspdf";

import type { ReportActivity, ReportDriver } from "./reportsService";
import type { DriverViolation } from "./violationsService";

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
    minor: "Minor",
    serious: "Serious",
    "very serious": "Very serious",
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
    return [firstName, lastName].filter(Boolean).join(" ") || "Brak danych";
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
            violation.violationType,
            formatDate(violation.occurredAtUtc),
            violation.description,
            severityLabels[violation.severity.toLowerCase()] ?? violation.severity,
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