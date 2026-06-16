import { useEffect, useMemo, useState, type FormEvent } from "react";

import Pagination from "../components/Pagination";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import { exportReportExcel } from "../services/excelExportService";
import {
    downloadDriverReport,
    getReportActivities,
    getReportDrivers,
    type ReportActivity,
    type ReportDriver,
} from "../services/reportsService";
import "../styles/reports.css";

const pageSize = 12;

const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
});

const activityLabels: Record<string, string> = {
    DRIVING: "Jazda",
    WORK: "Praca",
    REST: "Odpoczynek",
    AVAILABILITY: "Dyspozycyjność",
};

function formatDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateTimeFormatter.format(date);
}

function formatDateOnly(value: string) {
    if (!value) return "Nie wybrano";

    const date = new Date(`${value}T00:00:00`);

    return Number.isNaN(date.getTime())
        ? "Nie wybrano"
        : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return `${hours} godz. ${minutes.toString().padStart(2, "0")} min`;
}

function getDriverName(driver?: ReportDriver) {
    if (!driver) return "Wszyscy kierowcy";

    return `${driver.firstName} ${driver.lastName}`.trim() || "Kierowca bez nazwy";
}

function getActivityLabel(activityType: string) {
    const normalized = activityType.toUpperCase();

    return activityLabels[normalized] ?? (activityType || "Brak danych");
}

function getActivityClass(activityType: string) {
    const normalized = activityType.toUpperCase();

    if (normalized === "DRIVING") return "driving";
    if (normalized === "WORK") return "work";
    if (normalized === "REST") return "rest";
    if (normalized === "AVAILABILITY") return "availability";

    return "other";
}

function escapeCsv(value: string | number) {
    return `"${String(value).replaceAll('"', '""')}"`;
}

export default function ReportsPage() {
    const [drivers, setDrivers] = useState<ReportDriver[]>([]);
    const [activities, setActivities] = useState<ReportActivity[]>([]);
    const [driverCardNumber, setDriverCardNumber] = useState("");
    const [dateFrom, setDateFrom] = useState("");
    const [dateTo, setDateTo] = useState("");
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [error, setError] = useState("");
    const [currentPage, setCurrentPage] = useState(1);

    async function loadActivities(
        cardNumber = driverCardNumber,
        from = dateFrom,
        to = dateTo,
    ) {
        setIsLoading(true);
        setError("");

        try {
            const loadedActivities = await getReportActivities(cardNumber, from, to);
            setCurrentPage(1);
            setActivities(loadedActivities);
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania raportu.",
            );
        } finally {
            setIsLoading(false);
        }
    }

    useEffect(() => {
        async function loadInitialData() {
            setIsLoading(true);

            try {
                const [loadedDrivers, loadedActivities] = await Promise.all([
                    getReportDrivers(),
                    getReportActivities("", "", ""),
                ]);

                setCurrentPage(1);
                setDrivers(loadedDrivers);
                setActivities(loadedActivities);
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania raportu.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadInitialData();
    }, []);

    const selectedDriver = useMemo(
        () => drivers.find((item) => item.cardNumber === driverCardNumber),
        [driverCardNumber, drivers],
    );

    const dateRangeLabel = useMemo(() => {
        if (!dateFrom && !dateTo) return "Pełny dostępny zakres danych";
        if (dateFrom && dateTo) return `${formatDateOnly(dateFrom)} - ${formatDateOnly(dateTo)}`;
        if (dateFrom) return `Od ${formatDateOnly(dateFrom)}`;

        return `Do ${formatDateOnly(dateTo)}`;
    }, [dateFrom, dateTo]);

    const totals = useMemo(() => {
        const result = { driving: 0, rest: 0, work: 0, availability: 0 };

        for (const activity of activities) {
            const duration = Math.max(activity.durationSeconds, 0);

            switch (activity.activityType.toUpperCase()) {
                case "DRIVING":
                    result.driving += duration;
                    break;
                case "REST":
                    result.rest += duration;
                    break;
                case "WORK":
                    result.work += duration;
                    break;
                case "AVAILABILITY":
                    result.availability += duration;
                    break;
            }
        }

        return result;
    }, [activities]);

    const visibleActivities = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return activities.slice(start, start + pageSize);
    }, [activities, currentPage]);

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (dateFrom && dateTo && dateFrom > dateTo) {
            setError("Data początkowa nie może być późniejsza niż data końcowa.");
            return;
        }

        void loadActivities();
    }

    function exportCsv() {
        const rows = [
            [
                "Kierowca",
                "Numer karty",
                "Początek",
                "Koniec",
                "Typ aktywności",
                "Czas trwania",
                "Czas trwania (sekundy)",
            ],
            ...activities.map((activity) => [
                `${activity.driverFirstName} ${activity.driverLastName}`.trim() || "Brak danych",
                activity.driverCardNumber || "Brak danych",
                formatDate(activity.startUtc),
                formatDate(activity.endUtc),
                getActivityLabel(activity.activityType),
                formatDuration(activity.durationSeconds),
                activity.durationSeconds,
            ]),
        ];
        const csv = rows
            .map((row) => row.map(escapeCsv).join(";"))
            .join("\r\n");
        const blob = new Blob([`\uFEFF${csv}`], {
            type: "text/csv;charset=utf-8",
        });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");

        link.href = url;
        link.download = "raport-aktywnosci-kierowcow.csv";
        link.click();
        URL.revokeObjectURL(url);
    }

    async function handlePdfExport() {
        const driver = drivers.find(
            (item) => item.cardNumber === driverCardNumber,
        );

        if (!driver || !dateFrom || !dateTo) {
            setError("Wybierz kierowcę oraz pełny zakres dat przed eksportem.");
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data początkowa nie może być późniejsza niż data końcowa.");
            return;
        }

        setIsGeneratingPdf(true);
        setError("");

        try {
            await downloadDriverReport(driver.id, dateFrom, dateTo, "pdf");
        } catch (exportError) {
            setError(
                exportError instanceof Error
                    ? exportError.message
                    : "Nie udało się pobrać pliku PDF.",
            );
        } finally {
            setIsGeneratingPdf(false);
        }
    }

    async function handleExcelExport() {
        if (!selectedDriver || !dateFrom || !dateTo) {
            setError("Wybierz kierowcę oraz pełny zakres dat przed eksportem.");
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data początkowa nie może być późniejsza niż data końcowa.");
            return;
        }

        setIsGeneratingExcel(true);
        setError("");

        try {
            await exportReportExcel({
                activities,
                driver: selectedDriver,
                dateFrom,
                dateTo,
                totals,
            });
        } catch (exportError) {
            setError(
                exportError instanceof Error
                    ? exportError.message
                    : "Nie udało się wygenerować pliku Excel.",
            );
        } finally {
            setIsGeneratingExcel(false);
        }
    }

    return (
        <div className="reports-page">
            <section className="reports-hero">
                <div className="reports-hero-copy">
                    <span className="reports-eyebrow">Raport kierowcy</span>
                    <h2>Analiza aktywności z plików DDD</h2>
                    <p>
                        Sprawdź czas jazdy, pracy, odpoczynku i dyspozycyjności
                        dla wybranego kierowcy oraz zakresu dat.
                    </p>
                </div>
                <div className="reports-context-card" aria-label="Zakres raportu">
                    <span>Aktualny raport</span>
                    <strong>{getDriverName(selectedDriver)}</strong>
                    <dl>
                        <div>
                            <dt>Numer karty</dt>
                            <dd>{selectedDriver?.cardNumber || driverCardNumber || "Wszyscy kierowcy"}</dd>
                        </div>
                        <div>
                            <dt>Zakres dat</dt>
                            <dd>{dateRangeLabel}</dd>
                        </div>
                    </dl>
                </div>
            </section>

            <form className="reports-filters" onSubmit={handleSubmit}>
                <label>
                    Kierowca
                    <select
                        value={driverCardNumber}
                        onChange={(event) => setDriverCardNumber(event.target.value)}
                    >
                        <option value="">Wszyscy kierowcy</option>
                        {drivers.map((driver) => (
                            <option key={driver.id} value={driver.cardNumber}>
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
                        onChange={(event) => setDateFrom(event.target.value)}
                    />
                </label>

                <label>
                    Data do
                    <input
                        type="date"
                        value={dateTo}
                        onChange={(event) => setDateTo(event.target.value)}
                    />
                </label>

                <button type="submit" disabled={isLoading}>
                    {isLoading ? "Ładowanie..." : "Generuj raport"}
                </button>
            </form>

            {error && (
                <div className="reports-error" role="alert">
                    <strong>Nie można przygotować raportu</strong>
                    <span>{error}</span>
                </div>
            )}

            {isLoading && activities.length === 0 ? (
                <section className="report-summary report-summary-skeleton" aria-label="Ładowanie podsumowania">
                    {Array.from({ length: 4 }, (_, index) => (
                        <div className="ui-skeleton report-card-skeleton" key={index} />
                    ))}
                </section>
            ) : (
                <section className="report-summary" aria-label="Podsumowanie czasu">
                    <SummaryCard label="Jazda" seconds={totals.driving} tone="driving" />
                    <SummaryCard label="Praca" seconds={totals.work} tone="work" />
                    <SummaryCard label="Odpoczynek" seconds={totals.rest} tone="rest" />
                    <SummaryCard label="Dyspozycyjność" seconds={totals.availability} tone="availability" />
                </section>
            )}

            <section className="reports-panel">
                <div className="reports-panel-heading">
                    <div>
                        <span>Lista aktywności</span>
                        <h3>Aktywności w raporcie</h3>
                    </div>
                    <div className="reports-actions">
                        <span className="reports-count">{activities.length} rekordów</span>
                        <button
                            className="csv-button"
                            type="button"
                            onClick={exportCsv}
                            disabled={activities.length === 0 || isGeneratingPdf || isGeneratingExcel}
                        >
                            Eksport CSV
                        </button>
                        <button
                            className="pdf-button"
                            type="button"
                            onClick={() => void handlePdfExport()}
                            disabled={
                                !driverCardNumber
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
                                !driverCardNumber
                                || !dateFrom
                                || !dateTo
                                || activities.length === 0
                                || isGeneratingPdf
                                || isGeneratingExcel
                            }
                        >
                            {isGeneratingExcel ? "Generowanie Excel..." : "Eksport Excel"}
                        </button>
                    </div>
                </div>

                {isLoading && activities.length === 0 ? (
                    <TableSkeleton rows={7} columns={6} />
                ) : activities.length === 0 ? (
                    <EmptyState
                        title="Brak aktywności w raporcie"
                        description="Zmień kierowcę lub zakres dat. Po imporcie plików DDD aktywności pojawią się tutaj automatycznie."
                    />
                ) : (
                    <div className={isLoading ? "reports-content is-refreshing" : "reports-content"} aria-busy={isLoading}>
                        <div className="reports-table-wrapper">
                            <table className="reports-table">
                                <thead>
                                    <tr>
                                        <th>Kierowca</th>
                                        <th>Numer karty</th>
                                        <th>Początek</th>
                                        <th>Koniec</th>
                                        <th>Aktywność</th>
                                        <th>Czas</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {visibleActivities.map((activity) => (
                                        <tr key={activity.id}>
                                            <td data-label="Kierowca">
                                                <strong className="reports-driver-name">
                                                    {`${activity.driverFirstName} ${activity.driverLastName}`.trim() || "Brak danych"}
                                                </strong>
                                            </td>
                                            <td data-label="Numer karty">{activity.driverCardNumber || "Brak danych"}</td>
                                            <td data-label="Początek">{formatDate(activity.startUtc)}</td>
                                            <td data-label="Koniec">{formatDate(activity.endUtc)}</td>
                                            <td data-label="Aktywność">
                                                <span className={`activity-badge ${getActivityClass(activity.activityType)}`}>
                                                    {getActivityLabel(activity.activityType)}
                                                </span>
                                            </td>
                                            <td data-label="Czas">
                                                <strong>{formatDuration(activity.durationSeconds)}</strong>
                                            </td>
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
    tone: "driving" | "work" | "rest" | "availability";
};

function SummaryCard({ label, seconds, tone }: SummaryCardProps) {
    return (
        <article className={`report-summary-card ${tone}`}>
            <span>{label}</span>
            <strong>{formatDuration(seconds)}</strong>
            <small>Łączny czas w wybranym raporcie</small>
        </article>
    );
}
