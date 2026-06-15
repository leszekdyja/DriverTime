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

const severityLabels: Record<DriverViolation["severity"], string> = {
    low: "Niski",
    medium: "Sredni",
    high: "Wysoki",
};

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
    document.setFont("helvetica", "bold");
    document.text("DriverTime", 14, 13);
    document.setFontSize(11);
    document.setFont("helvetica", "normal");
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
    const driverName = options.driver
        ? getDriverName(options.driver.firstName, options.driver.lastName)
        : "Wszyscy kierowcy";
    const dateRange = `${options.dateFrom || "poczatek danych"} - ${options.dateTo || "koniec danych"}`;

    addHeader(document, "Raport aktywnosci kierowcow", `Zakres: ${dateRange}`);

    document.setFontSize(10);
    document.setFont("helvetica", "bold");
    document.text("Kierowca", 14, 41);
    document.text("Numer karty", 95, 41);
    document.text("Zakres dat", 176, 41);
    document.setFont("helvetica", "normal");
    document.text(driverName, 14, 47);
    document.text(options.driver?.cardNumber || "Wszystkie numery kart", 95, 47);
    document.text(dateRange, 176, 47);

    autoTable(document, {
        startY: 55,
        theme: "grid",
        head: [["Czas jazdy", "Czas pracy", "Czas odpoczynku", "Liczba aktywnosci"]],
        body: [[
            formatDuration(options.totals.driving),
            formatDuration(options.totals.work),
            formatDuration(options.totals.rest),
            String(options.activities.length),
        ]],
        styles: { fontSize: 9, cellPadding: 3 },
        headStyles: { fillColor: [37, 99, 235], textColor: 255 },
    });

    autoTable(document, {
        startY: 78,
        theme: "striped",
        head: [["Kierowca", "Numer karty", "Poczatek", "Koniec", "Aktywnosc", "Czas"]],
        body: options.activities.map((activity) => [
            getDriverName(activity.driverFirstName, activity.driverLastName),
            activity.driverCardNumber || "Brak danych",
            formatDate(activity.startUtc),
            formatDate(activity.endUtc),
            activity.activityType || "Brak danych",
            formatDuration(activity.durationSeconds),
        ]),
        styles: { fontSize: 8, cellPadding: 2.5, overflow: "linebreak" },
        headStyles: { fillColor: [15, 23, 42], textColor: 255 },
        alternateRowStyles: { fillColor: [241, 245, 249] },
        margin: { left: 14, right: 14 },
    });

    document.save("drivertime-activities-report.pdf");
}

export async function exportViolationsPdf(violations: DriverViolation[]) {
    const [{ jsPDF }, { default: autoTable }] = await Promise.all([
        import("jspdf"),
        import("jspdf-autotable"),
    ]);
    const document = new jsPDF({ orientation: "landscape", unit: "mm", format: "a4" });

    addHeader(document, "Raport naruszen czasu pracy", `${violations.length} naruszen`);

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
            severityLabels[violation.severity],
        ]),
        columnStyles: {
            0: { cellWidth: 38 },
            1: { cellWidth: 35 },
            2: { cellWidth: 45 },
            3: { cellWidth: 35 },
            4: { cellWidth: "auto" },
            5: { cellWidth: 22 },
        },
        styles: { fontSize: 8, cellPadding: 3, overflow: "linebreak" },
        headStyles: { fillColor: [15, 23, 42], textColor: 255 },
        alternateRowStyles: { fillColor: [241, 245, 249] },
        margin: { left: 14, right: 14 },
    });

    document.save("drivertime-violations-report.pdf");
}
