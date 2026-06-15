import { useEffect, useMemo, useState, type FormEvent } from "react";

import Pagination from "../components/Pagination";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getDriverReport,
    downloadDriverReport,
    getReportDrivers,
    type DriverReport,
    type ReportDriver,
} from "../services/reportsService";
import "../styles/reports.css";

const pageSize = 12;

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return `${hours} godz. ${minutes} min`;
}

function escapeCsv(value: string | number) {
    return `"${String(value).replaceAll('"', '""')}"`;
}

export default function ReportsPage() {
    const [drivers, setDrivers] = useState<ReportDriver[]>([]);
    const [report, setReport] = useState<DriverReport | null>(null);
    const [driverId, setDriverId] = useState("");
    const [dateFrom, setDateFrom] = useState("");
    const [dateTo, setDateTo] = useState("");
    const [isLoadingDrivers, setIsLoadingDrivers] = useState(true);
    const [isGeneratingReport, setIsGeneratingReport] = useState(false);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [error, setError] = useState("");
    const [currentPage, setCurrentPage] = useState(1);

    const activities = useMemo(() => report?.activities ?? [], [report]);

    async function loadReport() {
        setIsGeneratingReport(true);
        setError("");

        try {
            const loadedReport = await getDriverReport(driverId, dateFrom, dateTo);
            setCurrentPage(1);
            setReport(loadedReport);
        } catch (loadError) {
            setReport(null);
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystapil blad podczas pobierania raportu.",
            );
        } finally {
            setIsGeneratingReport(false);
        }
    }

    useEffect(() => {
        async function loadInitialData() {
            setIsLoadingDrivers(true);
            setError("");

            try {
                const loadedDrivers = await getReportDrivers();
                setDrivers(loadedDrivers);
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystapil blad podczas pobierania raportu.",
                );
            } finally {
                setIsLoadingDrivers(false);
            }
        }

        void loadInitialData();
    }, []);

    const totals = useMemo(() => {
        return {
            driving: report?.drivingSeconds ?? 0,
            rest: report?.restSeconds ?? 0,
            work: report?.workSeconds ?? 0,
        };
    }, [report]);

    const visibleActivities = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return activities.slice(start, start + pageSize);
    }, [activities, currentPage]);

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (dateFrom && dateTo && dateFrom > dateTo) {
            setError("Data poczatkowa nie moze byc pozniejsza niz data koncowa.");
            return;
        }

        if (!driverId || !dateFrom || !dateTo) {
            setError("Wybierz kierowce oraz pelny zakres dat.");
            return;
        }

        void loadReport();
    }

    function exportCsv() {
        const rows = [
            [
                "Kierowca",
                "Numer karty",
                "Poczatek",
                "Koniec",
                "Typ aktywnosci",
                "Czas trwania (sekundy)",
            ],
            ...activities.map((activity) => [
                report ? `${report.driverFirstName} ${report.driverLastName}`.trim() : "",
                report?.driverCardNumber ?? "",
                activity.startUtc,
                activity.endUtc,
                activity.activityType,
                activity.durationSeconds,
            ]),
        ];
        const csv = rows
            .map((row) => row.map(escapeCsv).join(","))
            .join("\r\n");
        const blob = new Blob([`\uFEFF${csv}`], {
            type: "text/csv;charset=utf-8",
        });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");

        link.href = url;
        link.download = "drivertime-activities-report.csv";
        link.click();
        URL.revokeObjectURL(url);
    }

    async function handlePdfExport() {
        if (!report || !driverId || !dateFrom || !dateTo) {
            setError("Najpierw wygeneruj raport dla kierowcy i zakresu dat.");
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data poczatkowa nie moze byc pozniejsza niz data koncowa.");
            return;
        }

        setIsGeneratingPdf(true);
        setError("");

        try {
            await downloadDriverReport(driverId, dateFrom, dateTo, "pdf");
        } catch (exportError) {
            setError(
                exportError instanceof Error
                    ? exportError.message
                    : "Nie udalo sie pobrac pliku PDF.",
            );
        } finally {
            setIsGeneratingPdf(false);
        }
    }

    async function handleExcelExport() {
        if (!report || !driverId || !dateFrom || !dateTo) {
            setError("Najpierw wygeneruj raport dla kierowcy i zakresu dat.");
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data poczatkowa nie moze byc pozniejsza niz data koncowa.");
            return;
        }

        setIsGeneratingExcel(true);
        setError("");

        try {
            await downloadDriverReport(driverId, dateFrom, dateTo, "excel");
        } catch (exportError) {
            setError(
                exportError instanceof Error
                    ? exportError.message
                    : "Nie udalo sie pobrac pliku Excel.",
            );
        } finally {
            setIsGeneratingExcel(false);
        }
    }

    return (
        <div className="reports-page">
            <div className="reports-heading">
                <div>
                    <h2>Raport aktywnosci</h2>
                    <p>Analiza czasu pracy kierowcow na podstawie importow DDD.</p>
                </div>
                <div className="reports-actions">
                    <button
                        className="csv-button"
                        type="button"
                        onClick={exportCsv}
                        disabled={activities.length === 0 || isGeneratingPdf || isGeneratingExcel}
                    >
                        Eksportuj CSV
                    </button>
                    <button
                        className="pdf-button"
                        type="button"
                        onClick={() => void handlePdfExport()}
                        disabled={
                            !report
                            || !dateFrom
                            || !dateTo
                            || isGeneratingPdf
                            || isGeneratingExcel
                        }
                    >
                        {isGeneratingPdf ? "Generowanie PDF..." : "Eksport PDF"}
                    </button>
                    <button
                        className="excel-button"
                        type="button"
                        onClick={() => void handleExcelExport()}
                        disabled={
                            !report
                            || !dateFrom
                            || !dateTo
                            || isGeneratingPdf
                            || isGeneratingExcel
                        }
                    >
                        {isGeneratingExcel ? "Generowanie Excel..." : "Eksport Excel"}
                    </button>
                </div>
            </div>

            <form className="reports-filters" onSubmit={handleSubmit}>
                <label>
                    Kierowca
                    <select
                        value={driverId}
                        onChange={(event) => {
                            setDriverId(event.target.value);
                            setReport(null);
                            setCurrentPage(1);
                        }}
                    >
                        <option value="">Wybierz kierowce</option>
                        {drivers.map((driver) => (
                            <option key={driver.id} value={driver.id}>
                                {driver.firstName} {driver.lastName} ({driver.cardNumber})
                            </option>
                        ))}
                    </select>
                </label>

                <label>
                    Data od
                    <input
                        type="date"
                        value={dateFrom}
                        onChange={(event) => {
                            setDateFrom(event.target.value);
                            setReport(null);
                            setCurrentPage(1);
                        }}
                    />
                </label>

                <label>
                    Data do
                    <input
                        type="date"
                        value={dateTo}
                        onChange={(event) => {
                            setDateTo(event.target.value);
                            setReport(null);
                            setCurrentPage(1);
                        }}
                    />
                </label>

                <button type="submit" disabled={isLoadingDrivers || isGeneratingReport}>
                    {isGeneratingReport ? "Generowanie..." : "Generuj raport"}
                </button>
            </form>

            {error && (
                <p className="reports-error" role="alert">
                    {error}
                </p>
            )}

            {isGeneratingReport && activities.length === 0 ? (
                <section className="report-summary report-summary-skeleton" aria-label="Ladowanie podsumowania">
                    {Array.from({ length: 3 }, (_, index) => <div className="ui-skeleton report-card-skeleton" key={index} />)}
                </section>
            ) : (
                <section className="report-summary" aria-label="Podsumowanie czasu">
                    <SummaryCard label="Czas jazdy" seconds={totals.driving} />
                    <SummaryCard label="Czas odpoczynku" seconds={totals.rest} />
                    <SummaryCard label="Czas pracy" seconds={totals.work} />
                </section>
            )}

            <section className="reports-panel">
                <div className="reports-panel-heading">
                    <h3>Aktywnosci</h3>
                    <span>{activities.length} rekordow</span>
                </div>

                {isGeneratingReport && activities.length === 0 ? (
                    <TableSkeleton rows={7} columns={6} />
                ) : !report ? (
                    <EmptyState
                        title="Wybierz parametry raportu"
                        description="Wybierz kierowce i zakres dat, a nastepnie kliknij Generuj raport."
                    />
                ) : activities.length === 0 ? (
                    <EmptyState
                        title="Brak danych raportu"
                        description="Dla wybranego kierowcy i zakresu dat nie znaleziono aktywnosci."
                    />
                ) : (
                    <div className={isGeneratingReport ? "reports-content is-refreshing" : "reports-content"} aria-busy={isGeneratingReport}>
                        <div className="reports-table-wrapper">
                            <table className="reports-table">
                            <thead>
                                <tr>
                                    <th>Kierowca</th>
                                    <th>Numer karty</th>
                                    <th>Poczatek</th>
                                    <th>Koniec</th>
                                    <th>Aktywnosc</th>
                                    <th>Czas</th>
                                </tr>
                            </thead>
                            <tbody>
                                {visibleActivities.map((activity, index) => (
                                    <tr key={`${activity.startUtc}-${activity.endUtc}-${activity.activityType}-${index}`}>
                                        <td>
                                            {`${report.driverFirstName} ${report.driverLastName}`.trim() || "Brak danych"}
                                        </td>
                                        <td>{report.driverCardNumber || "Brak danych"}</td>
                                        <td>{formatDate(activity.startUtc)}</td>
                                        <td>{formatDate(activity.endUtc)}</td>
                                        <td>{activity.activityType || "Brak danych"}</td>
                                        <td>{formatDuration(activity.durationSeconds)}</td>
                                    </tr>
                                ))}
                            </tbody>
                            </table>
                        </div>
                        <Pagination
                            currentPage={currentPage}
                            pageSize={pageSize}
                            totalItems={activities.length}
                            onPageChange={setCurrentPage}
                        />
                    </div>
                )}
            </section>
        </div>
    );
}

type SummaryCardProps = {
    label: string;
    seconds: number;
};

function SummaryCard({ label, seconds }: SummaryCardProps) {
    return (
        <article className="report-summary-card">
            <span>{label}</span>
            <strong>{formatDuration(seconds)}</strong>
        </article>
    );
}
