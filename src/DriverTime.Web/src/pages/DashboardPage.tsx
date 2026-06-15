import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import {
    getDashboardData,
    type DashboardData,
} from "../services/dashboardService";
import "../styles/dashboard.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) {
        return "Brak importow";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    if (hours === 0) {
        return `${minutes} min`;
    }

    return `${hours} godz. ${minutes} min`;
}

function displayValue(value: string) {
    return value || "Brak danych";
}

export default function DashboardPage() {
    const [dashboard, setDashboard] = useState<DashboardData | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        async function loadDashboard() {
            try {
                setDashboard(await getDashboardData());
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystapil blad podczas pobierania dashboardu.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadDashboard();
    }, []);

    const activityStatistics = useMemo(() => {
        if (!dashboard) {
            return [];
        }

        const statistics = new Map<string, { count: number; duration: number }>();

        for (const activity of dashboard.activities) {
            const type = activity.activityType || "Nieznana";
            const current = statistics.get(type) ?? { count: 0, duration: 0 };

            statistics.set(type, {
                count: current.count + 1,
                duration: current.duration + Math.max(activity.durationSeconds, 0),
            });
        }

        return Array.from(statistics, ([type, values]) => ({
            type,
            ...values,
        })).sort((first, second) => second.duration - first.duration);
    }, [dashboard]);

    if (isLoading) {
        return (
            <div className="dashboard-page">
                <h2>Dashboard</h2>
                <p className="dashboard-status" role="status">
                    Ladowanie dashboardu...
                </p>
            </div>
        );
    }

    if (error || !dashboard) {
        return (
            <div className="dashboard-page">
                <h2>Dashboard</h2>
                <p className="dashboard-error" role="alert">
                    {error || "Nie udalo sie wyswietlic dashboardu."}
                </p>
            </div>
        );
    }

    return (
        <div className="dashboard-page">
            <div className="dashboard-heading">
                <div>
                    <h2>Dashboard</h2>
                    <p>Podsumowanie danych transportowych DriverTime.</p>
                </div>
                <Link className="dashboard-action" to="/imports">
                    Przejdz do importow
                </Link>
            </div>

            <section className="dashboard-cards" aria-label="Podsumowanie">
                <SummaryCard label="Wszystkie importy" value={dashboard.totalImports} />
                <SummaryCard label="Kierowcy" value={dashboard.totalDrivers} />
                <SummaryCard label="Aktywnosci" value={dashboard.totalActivities} />
                <SummaryCard
                    label="Ostatni import"
                    value={formatDate(dashboard.latestImportDate)}
                    compact
                />
            </section>

            <div className="dashboard-grid">
                <section className="dashboard-panel latest-imports-panel">
                    <div className="panel-heading">
                        <h3>Ostatnie importy</h3>
                        <Link to="/imports">Zobacz wszystkie</Link>
                    </div>

                    {dashboard.latestImports.length === 0 ? (
                        <p>Brak zaimportowanych plikow DDD.</p>
                    ) : (
                        <div className="dashboard-table-wrapper">
                            <table className="dashboard-table">
                                <thead>
                                    <tr>
                                        <th>Plik</th>
                                        <th>Kierowca</th>
                                        <th>Data importu</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {dashboard.latestImports.map((dddImport) => (
                                        <tr key={dddImport.id}>
                                            <td>
                                                <Link to={`/imports/${dddImport.id}`}>
                                                    {displayValue(dddImport.fileName)}
                                                </Link>
                                            </td>
                                            <td>
                                                {displayValue(
                                                    [
                                                        dddImport.driverFirstName,
                                                        dddImport.driverLastName,
                                                    ]
                                                        .filter(Boolean)
                                                        .join(" "),
                                                )}
                                            </td>
                                            <td>{formatDate(dddImport.uploadedAtUtc)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </section>

                <section className="dashboard-panel activity-panel">
                    <div className="panel-heading">
                        <h3>Statystyki aktywnosci</h3>
                    </div>

                    {activityStatistics.length === 0 ? (
                        <p>Brak danych o aktywnosciach.</p>
                    ) : (
                        <div className="activity-statistics">
                            {activityStatistics.map((statistic) => (
                                <div className="activity-statistic" key={statistic.type}>
                                    <div>
                                        <strong>{statistic.type}</strong>
                                        <span>{statistic.count} zdarzen</span>
                                    </div>
                                    <span>{formatDuration(statistic.duration)}</span>
                                </div>
                            ))}
                        </div>
                    )}
                </section>
            </div>
        </div>
    );
}

type SummaryCardProps = {
    label: string;
    value: string | number;
    compact?: boolean;
};

function SummaryCard({ label, value, compact = false }: SummaryCardProps) {
    return (
        <article className="summary-card">
            <span>{label}</span>
            <strong className={compact ? "compact-value" : undefined}>{value}</strong>
        </article>
    );
}
