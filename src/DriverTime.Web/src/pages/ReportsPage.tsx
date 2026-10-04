import { useEffect, useMemo, useState, type FormEvent } from "react";

import Pagination from "../components/Pagination";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    downloadDriverReport,
    downloadCompanyReport,
    getCompanyReportActivities,
    getReportActivities,
    getReportDrivers,
    type ReportActivity,
    type ReportDriver,
} from "../services/reportsService";
import { getOperatingCompanies, type OperatingCompany } from "../services/operatingCompaniesService";
import { formatDriverNameOrFallback } from "../utils/driverName";
import "../styles/reports.css";

const pageSize = 12;
const defaultReportRangeDays = 60;

const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Europe/Warsaw",
});

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeZone: "Europe/Warsaw",
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

function formatActivityDate(value: string) {
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return `${hours} godz. ${minutes.toString().padStart(2, "0")} min`;
}

function formatNumber(value: number | null | undefined) {
    return value === null || value === undefined
        ? "Brak danych"
        : value.toLocaleString("pl-PL");
}

function formatKm(value: number | null | undefined) {
    return value === null || value === undefined
        ? "Brak danych"
        : `${value.toLocaleString("pl-PL")} km`;
}

function getDriverName(driver?: ReportDriver) {
    if (!driver) return "Wybierz kierowcę";

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
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function getDefaultDateRange() {
    const today = new Date();
    const from = new Date(today);
    from.setDate(today.getDate() - defaultReportRangeDays);

    return {
        from: toDateInputValue(from),
        to: toDateInputValue(today),
    };
}

export default function ReportsPage() {
    const defaultDateRange = useMemo(() => getDefaultDateRange(), []);
    const [drivers, setDrivers] = useState<ReportDriver[]>([]);
    const [companies, setCompanies] = useState<OperatingCompany[]>([]);
    const [activities, setActivities] = useState<ReportActivity[]>([]);
    const [reportScope, setReportScope] = useState<"driver" | "company">("driver");
    const [selectedDriverId, setSelectedDriverId] = useState("");
    const [selectedCompanyId, setSelectedCompanyId] = useState("");
    const [dateFrom, setDateFrom] = useState(defaultDateRange.from);
    const [dateTo, setDateTo] = useState(defaultDateRange.to);
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [error, setError] = useState("");
    const [currentPage, setCurrentPage] = useState(1);

    async function loadActivities(
        driverId = selectedDriverId,
        from = dateFrom,
        to = dateTo,
    ) {
        setIsLoading(true);
        setError("");

        try {
            let loadedActivities: ReportActivity[];
            if (reportScope === "company") {
                if (!selectedCompanyId) {
                    setError("Wybierz firmę przed wygenerowaniem raportu.");
                    setActivities([]);
                    return;
                }
                loadedActivities = await getCompanyReportActivities(selectedCompanyId, from, to);
            } else {
                const driver = drivers.find((item) => item.id === driverId);
                if (!driver) {
                    setError("Wybierz kierowcę przed wygenerowaniem raportu.");
                    setActivities([]);
                    return;
                }
                loadedActivities = await getReportActivities(driver.id, from, to, driver.cardNumber);
            }
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
                const [loadedDrivers, loadedCompanies] = await Promise.all([
                    getReportDrivers(),
                    getOperatingCompanies(),
                ]);

                setCurrentPage(1);
                setDrivers(loadedDrivers);
                setCompanies(loadedCompanies);
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
    const selectedCompany = useMemo(
        () => companies.find((item) => item.id === selectedCompanyId),
        [selectedCompanyId, companies],
    );

    const dateRangeLabel = useMemo(() => {
        if (!dateFrom && !dateTo) return "Pełny dostępny zakres danych";
        if (dateFrom && dateTo) return `${formatDateOnly(dateFrom)} - ${formatDateOnly(dateTo)}`;
        if (dateFrom) return `Od ${formatDateOnly(dateFrom)}`;

        return `Do ${formatDateOnly(dateTo)}`;
    }, [dateFrom, dateTo]);

    const totals = useMemo(() => {
        const result = { driving: 0, rest: 0, work: 0, availability: 0, distanceKm: 0, hasDistance: false };

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

            if (activity.distanceKm !== null && activity.distanceKm !== undefined) {
                result.distanceKm += activity.distanceKm;
                result.hasDistance = true;
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

        if (reportScope === "driver" ? !selectedDriverId : !selectedCompanyId) {
            setError(reportScope === "driver" ? "Wybierz kierowcę przed wygenerowaniem raportu." : "Wybierz firmę przed wygenerowaniem raportu.");
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
                "Przebieg początkowy",
                "Przebieg końcowy",
                "Km",
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
                formatNumber(activity.startOdometerKm),
                formatNumber(activity.endOdometerKm),
                formatNumber(activity.distanceKm),
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
        link.download = reportScope === "company"
            ? "raport-aktywnosci-firmy-z-kilometrami.csv"
            : "raport-aktywnosci-kierowcy-z-kilometrami.csv";
        link.click();
        URL.revokeObjectURL(url);
    }

    async function handlePdfExport() {
        if (!(reportScope === "driver" ? selectedDriver : selectedCompany) || !dateFrom || !dateTo) {
            setError(`Wybierz ${reportScope === "driver" ? "kierowcę" : "firmę"} oraz pełny zakres dat przed eksportem.`);
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data początkowa nie może być późniejsza niż data końcowa.");
            return;
        }

        setIsGeneratingPdf(true);
        setError("");

        try {
            if (reportScope === "company") {
                await downloadCompanyReport(selectedCompanyId, dateFrom, dateTo);
            } else {
                await downloadDriverReport(selectedDriverId, dateFrom, dateTo, "pdf");
            }
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
            await downloadDriverReport(selectedDriver.id, dateFrom, dateTo, "excel");
        } catch (exportError) {
            setError(
                exportError instanceof Error
                    ? exportError.message
                    : "Nie udało się pobrać pliku Excel.",
            );
        } finally {
            setIsGeneratingExcel(false);
        }
    }

    return (
        <div className="reports-page">
            <section className="reports-hero">
                <div className="reports-hero-copy">
                    <span className="reports-eyebrow">Raport {reportScope === "driver" ? "kierowcy" : "firmy"}</span>
                    <h2>Aktywności {reportScope === "driver" ? "kierowcy" : "kierowców firmy"} z kilometrami</h2>
                    <p>
                        Sprawdź czas jazdy, pracy, odpoczynku, dyspozycyjności oraz kilometry
                        przypisane do użyć pojazdu z plików DDD.
                    </p>
                </div>
                <div className="reports-context-card" aria-label="Zakres raportu">
                    <span>Aktualny raport</span>
                    <strong>{reportScope === "driver" ? getDriverName(selectedDriver) : selectedCompany?.name ?? "Wybierz firmę"}</strong>
                    <dl>
                        <div>
                            <dt>{reportScope === "driver" ? "Numer karty" : "Kierowcy"}</dt>
                            <dd>{reportScope === "driver" ? selectedDriver?.cardNumber || "Nie wybrano" : selectedCompany ? `${selectedCompany.driversCount} przypisanych` : "Nie wybrano"}</dd>
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
                    Typ raportu
                    <select value={reportScope} onChange={(event) => { setReportScope(event.target.value as "driver" | "company"); setActivities([]); setError(""); }}>
                        <option value="driver">Według kierowcy</option>
                        <option value="company">Według firmy</option>
                    </select>
                </label>
                {reportScope === "driver" ? (
                <label>
                    Kierowca
                    <select
                        value={selectedDriverId}
                        onChange={(event) => setSelectedDriverId(event.target.value)}
                    >
                        <option value="">Wybierz kierowcę</option>
                        {drivers.map((driver) => (
                            <option key={driver.id} value={driver.id}>
                                {formatDriverNameOrFallback(driver.firstName, driver.lastName)} ({driver.cardNumber})
                            </option>
                        ))}
                    </select>
                </label>
                ) : (
                    <label>
                        Firma
                        <select value={selectedCompanyId} onChange={(event) => setSelectedCompanyId(event.target.value)}>
                            <option value="">Wybierz firmę</option>
                            {companies.map((company) => <option key={company.id} value={company.id}>{company.name} ({company.driversCount})</option>)}
                        </select>
                    </label>
                )}

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
                    {Array.from({ length: 5 }, (_, index) => (
                        <div className="ui-skeleton report-card-skeleton" key={index} />
                    ))}
                </section>
            ) : (
                <section className="report-summary report-summary-five" aria-label="Podsumowanie raportu">
                    <SummaryCard label="Jazda" seconds={totals.driving} tone="driving" />
                    <SummaryCard label="Praca" seconds={totals.work} tone="work" />
                    <SummaryCard label="Odpoczynek" seconds={totals.rest} tone="rest" />
                    <SummaryCard label="Dyspozycyjność" seconds={totals.availability} tone="availability" />
                    <DistanceSummaryCard distanceKm={totals.hasDistance ? totals.distanceKm : null} />
                </section>
            )}

            <section className="reports-panel">
                <div className="reports-panel-heading">
                    <div>
                        <span>Lista aktywności</span>
                        <h3>Aktywności, pojazdy i kilometry</h3>
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
                                (reportScope === "driver" ? !selectedDriverId : !selectedCompanyId)
                                || !dateFrom
                                || !dateTo
                                || isGeneratingPdf
                                || isGeneratingExcel
                            }
                        >
                            {isGeneratingPdf ? "Generowanie PDF..." : "Eksport PDF / druk"}
                        </button>
                        <button
                            className="excel-button"
                            type="button"
                            title={reportScope === "company" ? "Raport firmowy jest dostępny do druku jako PDF oraz CSV." : undefined}
                            onClick={() => void handleExcelExport()}
                            disabled={
                                reportScope === "company"
                                || !selectedDriverId
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
                    <TableSkeleton rows={7} columns={10} />
                ) : activities.length === 0 ? (
                    <EmptyState
                        title="Brak aktywności w raporcie"
                        description={`Wybierz ${reportScope === "driver" ? "kierowcę" : "firmę"} i zakres dat. Po imporcie plików DDD aktywności pojawią się tutaj automatycznie.`}
                    />
                ) : (
                    <div className={isLoading ? "reports-content is-refreshing" : "reports-content"} aria-busy={isLoading}>
                        <div className="reports-table-wrapper">
                            <table className="reports-table reports-table-wide">
                                <thead>
                                    <tr>
                                        <th>Data</th>
                                        {reportScope === "company" && <th>Kierowca</th>}
                                        <th>Od</th>
                                        <th>Do</th>
                                        <th>Aktywność</th>
                                        <th>Pojazd</th>
                                        <th>Czas trwania</th>
                                        <th>Przebieg początkowy</th>
                                        <th>Przebieg końcowy</th>
                                        <th>Km</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {visibleActivities.map((activity) => (
                                        <tr key={activity.id}>
                                            <td data-label="Data">{formatActivityDate(activity.startUtc)}</td>
                                            {reportScope === "company" && <td data-label="Kierowca">{formatDriverNameOrFallback(activity.driverFirstName, activity.driverLastName)}</td>}
                                            <td data-label="Od">{formatDate(activity.startUtc)}</td>
                                            <td data-label="Do">{formatDate(activity.endUtc)}</td>
                                            <td data-label="Aktywność">
                                                <span className={`activity-badge ${getActivityClass(activity.activityType)}`}>
                                                    {getActivityLabel(activity.activityType)}
                                                </span>
                                            </td>
                                            <td data-label="Pojazd">{getVehicle(activity)}</td>
                                            <td data-label="Czas trwania">
                                                <strong>{formatDuration(activity.durationSeconds)}</strong>
                                            </td>
                                            <td data-label="Przebieg początkowy">{formatNumber(activity.startOdometerKm)}</td>
                                            <td data-label="Przebieg końcowy">{formatNumber(activity.endOdometerKm)}</td>
                                            <td data-label="Km">
                                                <strong>{formatKm(activity.distanceKm)}</strong>
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

function DistanceSummaryCard({ distanceKm }: { distanceKm: number | null }) {
    return (
        <article className="report-summary-card distance">
            <span>Kilometry</span>
            <strong>{formatKm(distanceKm)}</strong>
            <small>Łączny dystans z dostępnych danych</small>
        </article>
    );
}
