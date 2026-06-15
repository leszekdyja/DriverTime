import { useEffect, useMemo, useState, type FormEvent } from "react";

import {
    downloadDriverReport,
    getReportActivities,
    getReportDrivers,
    type ReportActivity,
    type ReportDriver,
} from "../services/reportsService";
import "../styles/reports.css";

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
    const [activities, setActivities] = useState<ReportActivity[]>([]);
    const [driverCardNumber, setDriverCardNumber] = useState("");
    const [dateFrom, setDateFrom] = useState("");
    const [dateTo, setDateTo] = useState("");
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [error, setError] = useState("");

    async function loadActivities(
        cardNumber = driverCardNumber,
        from = dateFrom,
        to = dateTo,
    ) {
        setIsLoading(true);
        setError("");

        try {
            setActivities(await getReportActivities(cardNumber, from, to));
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystapil blad podczas pobierania raportu.",
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

                setDrivers(loadedDrivers);
                setActivities(loadedActivities);
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystapil blad podczas pobierania raportu.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadInitialData();
    }, []);

    const totals = useMemo(() => {
        const result = { driving: 0, rest: 0, work: 0 };

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
            }
        }

        return result;
    }, [activities]);

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (dateFrom && dateTo && dateFrom > dateTo) {
            setError("Data poczatkowa nie moze byc pozniejsza niz data koncowa.");
            return;
        }

        void loadActivities();
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
                `${activity.driverFirstName} ${activity.driverLastName}`.trim(),
                activity.driverCardNumber,
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
        const driver = drivers.find(
            (item) => item.cardNumber === driverCardNumber,
        );

        if (!driver || !dateFrom || !dateTo) {
            setError("Wybierz kierowce oraz pelny zakres dat przed eksportem.");
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data poczatkowa nie moze byc pozniejsza niz data koncowa.");
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
                    : "Nie udalo sie pobrac pliku PDF.",
            );
        } finally {
            setIsGeneratingPdf(false);
        }
    }

    async function handleExcelExport() {
        const driver = drivers.find(
            (item) => item.cardNumber === driverCardNumber,
        );

        if (!driver || !dateFrom || !dateTo) {
            setError("Wybierz kierowce oraz pelny zakres dat przed eksportem.");
            return;
        }

        if (dateFrom > dateTo) {
            setError("Data poczatkowa nie moze byc pozniejsza niz data koncowa.");
            return;
        }

        setIsGeneratingExcel(true);
        setError("");

        try {
            await downloadDriverReport(driver.id, dateFrom, dateTo, "excel");
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
                    {isLoading ? "Ladowanie..." : "Generuj raport"}
                </button>
            </form>

            {error && (
                <p className="reports-error" role="alert">
                    {error}
                </p>
            )}

            <section className="report-summary" aria-label="Podsumowanie czasu">
                <SummaryCard label="Czas jazdy" seconds={totals.driving} />
                <SummaryCard label="Czas odpoczynku" seconds={totals.rest} />
                <SummaryCard label="Czas pracy" seconds={totals.work} />
            </section>

            <section className="reports-panel">
                <div className="reports-panel-heading">
                    <h3>Aktywnosci</h3>
                    <span>{activities.length} rekordow</span>
                </div>

                {isLoading ? (
                    <p className="reports-status" role="status">
                        Ladowanie danych raportu...
                    </p>
                ) : activities.length === 0 ? (
                    <p>Brak aktywnosci dla wybranych filtrow.</p>
                ) : (
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
                                {activities.map((activity) => (
                                    <tr key={activity.id}>
                                        <td>
                                            {`${activity.driverFirstName} ${activity.driverLastName}`.trim() || "Brak danych"}
                                        </td>
                                        <td>{activity.driverCardNumber || "Brak danych"}</td>
                                        <td>{formatDate(activity.startUtc)}</td>
                                        <td>{formatDate(activity.endUtc)}</td>
                                        <td>{activity.activityType || "Brak danych"}</td>
                                        <td>{formatDuration(activity.durationSeconds)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
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
