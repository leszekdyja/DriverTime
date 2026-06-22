import { useEffect, useMemo, useState, type FormEvent } from "react";

import Pagination from "../components/Pagination";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import { exportReportExcel } from "../services/excelExportService";
import {
    downloadDriverReport,
    getDriverMileageReport,
    getReportActivities,
    getReportDrivers,
    type DriverMileageReport,
    type ReportActivity,
    type ReportDriver,
} from "../services/reportsService";
import { formatDriverNameOrFallback } from "../utils/driverName";
import "../styles/reports.css";

const pageSize = 12;
const defaultReportRangeDays = 60;

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

function formatReportDateOnly(value: string) {
    if (!value) return "Brak danych";

    const date = new Date(`${value}T00:00:00`);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return `${hours} godz. ${minutes.toString().padStart(2, "0")} min`;
}

function formatKm(value: number | null | undefined) {
    return value === null || value === undefined
        ? "Brak danych"
        : `${value.toLocaleString("pl-PL")} km`;
}

function formatPlainNumber(value: number | null | undefined) {
    return value === null || value === undefined
        ? "Brak danych"
        : value.toLocaleString("pl-PL");
}

function getDriverName(driver?: ReportDriver) {
    if (!driver) return "Wszyscy kierowcy";

    return formatDriverNameOrFallback(driver.firstName, driver.lastName, "Kierowca bez nazwy");
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

function getVehicle(activity: ReportActivity) {
    return activity.vehicleRegistration
        || activity.vehicleRegistrationNumber
        || activity.vehicle
        || "Brak danych";
}

function escapeCsv(value: string | number) {
    return `"${String(value).replaceAll('"', '""')}"`;
}

function toDateInputValue(date: Date) {
    return date.toISOString().slice(0, 10);
}

function getDefaultDateRange() {
    const today = new Date();
    const from = new Date(today);
    from.setUTCDate(today.getUTCDate() - defaultReportRangeDays);

    return {
        from: toDateInputValue(from),
        to: toDateInputValue(today),
    };
}

export default function ReportsPage() {
    const defaultDateRange = useMemo(() => getDefaultDateRange(), []);
    const [drivers, setDrivers] = useState<ReportDriver[]>([]);
    const [activities, setActivities] = useState<ReportActivity[]>([]);
    const [selectedDriverId, setSelectedDriverId] = useState("");
    const [dateFrom, setDateFrom] = useState(defaultDateRange.from);
    const [dateTo, setDateTo] = useState(defaultDateRange.to);
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [error, setError] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const [selectedMileageDriverId, setSelectedMileageDriverId] = useState("");
    const [mileageDateFrom, setMileageDateFrom] = useState(defaultDateRange.from);
    const [mileageDateTo, setMileageDateTo] = useState(defaultDateRange.to);
    const [mileageReport, setMileageReport] = useState<DriverMileageReport | null>(null);
    const [isMileageLoading, setIsMileageLoading] = useState(false);
    const [mileageError, setMileageError] = useState("");
    const [hasRequestedMileageReport, setHasRequestedMileageReport] = useState(false);

    async function loadActivities(
        driverId = selectedDriverId,
        from = dateFrom,
        to = dateTo,
    ) {
        setIsLoading(true);
        setError("");

        try {
            const driver = drivers.find((item) => item.id === driverId);

            if (!driver) {
                setError("Wybierz kierowcę przed wygenerowaniem raportu.");
                setActivities([]);
                return;
            }

            const loadedActivities = await getReportActivities(driver.id, from, to, driver.cardNumber);
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
                const loadedDrivers = await getReportDrivers();

                setCurrentPage(1);
                setDrivers(loadedDrivers);
                setActivities([]);
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
        () => drivers.find((item) => item.id === selectedDriverId),
        [selectedDriverId, drivers],
    );

    const selectedMileageDriver = useMemo(
        () => drivers.find((item) => item.id === selectedMileageDriverId),
        [selectedMileageDriverId, drivers],
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

        if (!selectedDriverId) {
            setError("Wybierz kierowcę przed wygenerowaniem raportu.");
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
                "Pojazd",
            ],
            ...activities.map((activity) => [
                formatDriverNameOrFallback(activity.driverFirstName, activity.driverLastName),
                activity.driverCardNumber || "Brak danych",
                formatDate(activity.startUtc),
                formatDate(activity.endUtc),
                getActivityLabel(activity.activityType),
                formatDuration(activity.durationSeconds),
                activity.durationSeconds,
                getVehicle(activity),
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
        const driver = selectedDriver;

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

    async function handleMileageSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (!selectedMileageDriverId) {
            setMileageError("Wybierz kierowcę przed wygenerowaniem raportu kilometrów.");
            setMileageReport(null);
            setHasRequestedMileageReport(true);
            return;
        }

        if (!mileageDateFrom || !mileageDateTo) {
            setMileageError("Wybierz pełny zakres dat raportu kilometrów.");
            setMileageReport(null);
            setHasRequestedMileageReport(true);
            return;
        }

        if (mileageDateFrom > mileageDateTo) {
            setMileageError("Data początkowa nie może być późniejsza niż data końcowa.");
            setMileageReport(null);
            setHasRequestedMileageReport(true);
            return;
        }

        setIsMileageLoading(true);
        setMileageError("");
        setHasRequestedMileageReport(true);

        try {
            const report = await getDriverMileageReport(
                selectedMileageDriverId,
                mileageDateFrom,
                mileageDateTo,
            );
            setMileageReport(report);
        } catch (loadError) {
            setMileageReport(null);
            setMileageError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania raportu kilometrów.",
            );
        } finally {
            setIsMileageLoading(false);
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
                            <dd>{selectedDriver?.cardNumber || "Nie wybrano"}</dd>
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
                        value={selectedDriverId}
                        onChange={(event) => setSelectedDriverId(event.target.value)}
                    >
                        <option value="">Wszyscy kierowcy</option>
                        {drivers.map((driver) => (
                            <option key={driver.id} value={driver.id}>
                                {formatDriverNameOrFallback(driver.firstName, driver.lastName)} ({driver.cardNumber})
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

            <section className="reports-panel mileage-report-panel">
                <div className="reports-panel-heading">
                    <div>
                        <span>Raport kilometrów</span>
                        <h3>Kilometry kierowcy z danych pojazdów</h3>
                    </div>
                    <span className="reports-count">
                        {mileageReport ? `${mileageReport.rows.length} wierszy` : "Nie wygenerowano"}
                    </span>
                </div>

                <form className="reports-filters mileage-filters" onSubmit={handleMileageSubmit}>
                    <label>
                        Kierowca
                        <select
                            value={selectedMileageDriverId}
                            onChange={(event) => {
                                setSelectedMileageDriverId(event.target.value);
                                setMileageReport(null);
                                setMileageError("");
                            }}
                        >
                            <option value="">Wybierz kierowcę</option>
                            {drivers.map((driver) => (
                                <option key={driver.id} value={driver.id}>
                                    {formatDriverNameOrFallback(driver.firstName, driver.lastName)} ({driver.cardNumber})
                                </option>
                            ))}
                        </select>
                    </label>

                    <label>
                        Data od
                        <input
                            type="date"
                            value={mileageDateFrom}
                            onChange={(event) => {
                                setMileageDateFrom(event.target.value);
                                setMileageReport(null);
                                setMileageError("");
                            }}
                        />
                    </label>

                    <label>
                        Data do
                        <input
                            type="date"
                            value={mileageDateTo}
                            onChange={(event) => {
                                setMileageDateTo(event.target.value);
                                setMileageReport(null);
                                setMileageError("");
                            }}
                        />
                    </label>

                    <button type="submit" disabled={isMileageLoading || drivers.length === 0}>
                        {isMileageLoading ? "Ładowanie..." : "Generuj raport"}
                    </button>
                </form>

                {mileageError && (
                    <div className="reports-error" role="alert">
                        <strong>Nie można przygotować raportu kilometrów</strong>
                        <span>{mileageError}</span>
                    </div>
                )}

                {isMileageLoading ? (
                    <TableSkeleton rows={5} columns={8} />
                ) : mileageReport ? (
                    <div className="mileage-report-results">
                        <div className="mileage-report-meta" aria-label="Informacje o raporcie kilometrów">
                            <div>
                                <span>Kierowca</span>
                                <strong>
                                    {mileageReport.driverName
                                        || getDriverName(selectedMileageDriver)
                                        || "Brak danych"}
                                </strong>
                            </div>
                            <div>
                                <span>Zakres dat</span>
                                <strong>
                                    {formatReportDateOnly(mileageReport.from)} - {formatReportDateOnly(mileageReport.to)}
                                </strong>
                            </div>
                        </div>

                        <section className="report-summary mileage-summary" aria-label="Podsumowanie kilometrów">
                            <article className="report-summary-card driving">
                                <span>Suma kilometrów</span>
                                <strong>{formatKm(mileageReport.totalDistanceKm)}</strong>
                                <small>Tylko rekordy z danymi dystansu</small>
                            </article>
                            <article className="report-summary-card work">
                                <span>Użycia pojazdu</span>
                                <strong>{mileageReport.vehicleUseCount.toLocaleString("pl-PL")}</strong>
                                <small>Liczba zapisanych okresów pojazdu</small>
                            </article>
                            <article className="report-summary-card availability">
                                <span>Braki dystansu</span>
                                <strong>{mileageReport.missingDistanceCount.toLocaleString("pl-PL")}</strong>
                                <small>Rekordy bez wartości DistanceKm</small>
                            </article>
                        </section>

                        {mileageReport.rows.length === 0 ? (
                            <EmptyState
                                title="Brak danych kilometrów"
                                description="Dla wybranego kierowcy i zakresu dat nie znaleziono użyć pojazdu."
                            />
                        ) : (
                            <div className="reports-table-wrapper mileage-table-wrapper">
                                <table className="reports-table mileage-table">
                                    <thead>
                                        <tr>
                                            <th>Data</th>
                                            <th>Od</th>
                                            <th>Do</th>
                                            <th>Pojazd</th>
                                            <th>Licznik początkowy</th>
                                            <th>Licznik końcowy</th>
                                            <th>Dystans km</th>
                                            <th>Status danych</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {mileageReport.rows.map((row) => (
                                            <tr key={`${row.startUtc}-${row.endUtc}-${row.registrationNumber}`}>
                                                <td data-label="Data">{formatReportDateOnly(row.date)}</td>
                                                <td data-label="Od">{formatDate(row.startUtc)}</td>
                                                <td data-label="Do">{formatDate(row.endUtc)}</td>
                                                <td data-label="Pojazd">{row.registrationNumber || "Brak danych"}</td>
                                                <td data-label="Licznik początkowy">{formatPlainNumber(row.startOdometerKm)}</td>
                                                <td data-label="Licznik końcowy">{formatPlainNumber(row.endOdometerKm)}</td>
                                                <td data-label="Dystans km">
                                                    <strong>{formatKm(row.distanceKm)}</strong>
                                                </td>
                                                <td data-label="Status danych">
                                                    <span className={`mileage-status ${row.hasDistanceData ? "ok" : "missing"}`}>
                                                        {row.hasDistanceData ? "OK" : "Brak danych dystansu"}
                                                    </span>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </div>
                ) : hasRequestedMileageReport ? (
                    <EmptyState
                        title="Brak danych kilometrów"
                        description="Zmień kierowcę lub zakres dat i wygeneruj raport ponownie."
                    />
                ) : (
                    <EmptyState
                        title="Raport kilometrów nie został jeszcze wygenerowany"
                        description="Wybierz kierowcę i zakres dat, a następnie kliknij „Generuj raport”."
                    />
                )}
            </section>

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
                                !selectedDriverId
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
                                !selectedDriverId
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
                                        <th>Pojazd</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {visibleActivities.map((activity) => (
                                        <tr key={activity.id}>
                                            <td data-label="Kierowca">
                                                <strong className="reports-driver-name">
                                                    {formatDriverNameOrFallback(activity.driverFirstName, activity.driverLastName)}
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
                                            <td data-label="Pojazd">{getVehicle(activity)}</td>
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
