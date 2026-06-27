import { useCallback, useEffect, useState } from "react";

import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getRecentImportMonitoring,
    retryFailedImportMonitoringEntries,
    retryImportMonitoringEntry,
    type ImportMonitoringEntry,
} from "../services/importMonitoringService";
import "../styles/imports.css";
import "../styles/import-monitoring.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) {
        return "Brak danych";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function translateStatus(status: string) {
    const labels: Record<string, string> = {
        Pending: "Oczekuje",
        Processing: "Przetwarzanie",
        Completed: "Zakończony",
        Failed: "Błąd",
    };

    return labels[status] ?? status;
}

function getStatusClass(status: string) {
    const normalized = status.toLowerCase();

    if (normalized === "completed") return "completed";
    if (normalized === "failed") return "failed";
    if (normalized === "duplicate") return "duplicate";
    if (normalized === "processing") return "processing";

    return "pending";
}

export default function ImportMonitoringPage() {
    const [entries, setEntries] = useState<ImportMonitoringEntry[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [retryingId, setRetryingId] = useState<string | null>(null);
    const [isRetryingFailed, setIsRetryingFailed] = useState(false);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [lastRefreshAt, setLastRefreshAt] = useState<Date | null>(null);

    const loadMonitoring = useCallback(async (silent = false) => {
        if (silent) {
            setIsRefreshing(true);
        } else {
            setIsLoading(true);
        }

        setError("");

        try {
            setEntries(await getRecentImportMonitoring(20));
            setLastRefreshAt(new Date());
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania monitoringu importów.",
            );
        } finally {
            setIsLoading(false);
            setIsRefreshing(false);
        }
    }, []);

    useEffect(() => {
        void loadMonitoring();

        const intervalId = window.setInterval(() => {
            void loadMonitoring(true);
        }, 5000);

        return () => window.clearInterval(intervalId);
    }, [loadMonitoring]);

    async function handleRetry(id: string) {
        setRetryingId(id);
        setError("");
        setMessage("");

        try {
            await retryImportMonitoringEntry(id);
            setMessage("Import został przekazany do ponowienia.");
            await loadMonitoring(true);
        } catch (retryError) {
            setError(
                retryError instanceof Error
                    ? retryError.message
                    : "Nie udało się ponowić importu.",
            );
        } finally {
            setRetryingId(null);
        }
    }

    async function handleRetryFailed() {
        setIsRetryingFailed(true);
        setError("");
        setMessage("");

        try {
            await retryFailedImportMonitoringEntries();
            setMessage("Błędne importy zostały przekazane do ponowienia.");
            await loadMonitoring(true);
        } catch (retryError) {
            setError(
                retryError instanceof Error
                    ? retryError.message
                    : "Nie udało się ponowić błędnych importów.",
            );
        } finally {
            setIsRetryingFailed(false);
        }
    }

    return (
        <div className="imports-page import-monitoring-page">
            <div className="import-monitoring-header">
                <div>
                    <h2>Monitoring importów DDD</h2>
                    <p>Podgląd ostatnich zadań importu oraz statusów przetwarzania.</p>
                </div>
                <div className="import-monitoring-meta">
                    <button
                        className="retry-failed-button"
                        type="button"
                        onClick={() => void handleRetryFailed()}
                        disabled={isRetryingFailed}
                    >
                        {isRetryingFailed ? "Ponawianie..." : "Ponów błędne importy"}
                    </button>
                    <span>{isRefreshing ? "Odświeżanie..." : "Auto refresh: 5 s"}</span>
                    {lastRefreshAt && (
                        <small>Ostatnio: {dateFormatter.format(lastRefreshAt)}</small>
                    )}
                </div>
            </div>

            <section className="imports-list">
                <div className="imports-list-heading">
                    <div>
                        <h3>Ostatnie importy</h3>
                        <p>Statusy pochodzą z backendowego monitoringu importów DDD.</p>
                    </div>
                    {!isLoading && !error && (
                        <span>{entries.length} wpisów</span>
                    )}
                </div>

                {isLoading && entries.length === 0 && <TableSkeleton rows={5} columns={6} />}

                {error && (
                    <p className="message error-message" role="alert">
                        {error}
                    </p>
                )}

                {message && (
                    <p className="message success-message" role="status">
                        {message}
                    </p>
                )}

                {!isLoading && !error && entries.length === 0 && (
                    <EmptyState
                        title="Brak wpisów monitoringu"
                        description="Po uruchomieniu importu DDD pojawi się tutaj status przetwarzania pliku."
                    />
                )}

                {!error && entries.length > 0 && (
                    <div
                        className={isRefreshing ? "imports-table-wrapper is-refreshing" : "imports-table-wrapper"}
                        aria-busy={isRefreshing}
                    >
                        <table className="imports-table import-monitoring-table">
                            <thead>
                                <tr>
                                    <th>Nazwa pliku</th>
                                    <th>Status</th>
                                    <th>Start</th>
                                    <th>Koniec</th>
                                    <th>Retry</th>
                                    <th>Błąd</th>
                                    <th>Akcje</th>
                                </tr>
                            </thead>
                            <tbody>
                                {entries.map((entry) => (
                                    <tr key={entry.id}>
                                        <td>{entry.fileName || "Brak danych"}</td>
                                        <td>
                                            <span className={`import-monitoring-status ${getStatusClass(entry.status)}`}>
                                                {translateStatus(entry.status)}
                                            </span>
                                        </td>
                                        <td>{formatDate(entry.startedAtUtc)}</td>
                                        <td>{formatDate(entry.finishedAtUtc)}</td>
                                        <td>{entry.retryCount ?? 0} / 3</td>
                                        <td className="import-monitoring-error">
                                            {entry.lastError || entry.errorMessage || "Brak"}
                                        </td>
                                        <td>
                                            {entry.status === "Failed" ? (
                                                <button
                                                    className="retry-import-button"
                                                    type="button"
                                                    onClick={() => void handleRetry(entry.id)}
                                                    disabled={retryingId !== null || isRetryingFailed}
                                                >
                                                    {retryingId === entry.id ? "Ponawianie..." : "Ponów"}
                                                </button>
                                            ) : (
                                                <span className="import-monitoring-muted">-</span>
                                            )}
                                        </td>
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
