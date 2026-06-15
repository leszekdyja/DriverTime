import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import DriverRiskOverview from "../components/DriverRiskOverview";
import {
    getDashboardData,
    type DashboardData,
} from "../services/dashboardService";
import "../styles/dashboard.css";

const ActivityBarChart = lazy(
    () => import("../components/ActivityBarChart"),
);

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const activityDefinitions = [
    { type: "DRIVING", label: "Jazda", color: "#2563eb" },
    { type: "WORK", label: "Praca", color: "#f59e0b" },
    { type: "REST", label: "Odpoczynek", color: "#16a34a" },
    { type: "AVAILABILITY", label: "Dyspozycyjnosc", color: "#8b5cf6" },
] as const;

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

    return hours === 0 ? `${minutes} min` : `${hours} godz. ${minutes} min`;
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
        return activityDefinitions.map((definition) => {
            const matchingActivities =
                dashboard?.activities.filter(
                    (activity) =>
                        activity.activityType.toUpperCase() === definition.type,
                ) ?? [];
            const duration = matchingActivities.reduce(
                (total, activity) =>
                    total + Math.max(activity.durationSeconds, 0),
                0,
            );

            return {
                ...definition,
                count: matchingActivities.length,
                duration,
                hours: Number((duration / 3600).toFixed(2)),
            };
        });
    }, [dashboard]);

    if (isLoading) {
        return <DashboardSkeleton />;
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

    const hasActivityData = activityStatistics.some(
        (statistic) => statistic.count > 0,
    );

    return (
        <div className="dashboard-page">
            <div className="dashboard-heading">
                <div>
                    <h2>Dashboard analityczny</h2>
                    <p>Najwazniejsze dane operacyjne i aktywnosci kierowcow.</p>
                </div>
                <Link className="dashboard-action" to="/imports">
                    Dodaj import DDD
                </Link>
            </div>

            <section className="dashboard-cards" aria-label="Podsumowanie">
                <SummaryCard label="Importy" value={dashboard.totalImports} accent="blue" />
                <SummaryCard label="Kierowcy" value={dashboard.totalDrivers} accent="cyan" />
                <SummaryCard label="Aktywnosci" value={dashboard.totalActivities} accent="amber" />
                <SummaryCard label="Pojazdy" value={dashboard.totalVehicles} accent="violet" />
                <SummaryCard
                    label="Ostatni import"
                    value={formatDate(dashboard.latestImportDate)}
                    accent="green"
                    compact
                />
            </section>

            <section className="activity-overview">
                <div className="panel-heading">
                    <div>
                        <h3>Statystyki aktywnosci</h3>
                        <p>Laczny czas wedlug rodzaju aktywnosci.</p>
                    </div>
                </div>

                <div className="activity-metrics">
                    {activityStatistics.map((statistic) => (
                        <article className="activity-metric" key={statistic.type}>
                            <span
                                className="activity-dot"
                                style={{ backgroundColor: statistic.color }}
                            />
                            <div>
                                <span>{statistic.label}</span>
                                <strong>{formatDuration(statistic.duration)}</strong>
                                <small>{statistic.count} zdarzen</small>
                            </div>
                        </article>
                    ))}
                </div>
            </section>

            <DriverRiskOverview />

            <div className="analytics-grid">
                <section className="dashboard-panel chart-panel">
                    <div className="panel-heading">
                        <div>
                            <h3>Rozklad czasu aktywnosci</h3>
                            <p>Czas w godzinach.</p>
                        </div>
                    </div>

                    {hasActivityData ? (
                        <div className="activity-chart" aria-label="Wykres aktywnosci">
                            <Suspense fallback={<div className="skeleton chart-skeleton" />}>
                                <ActivityBarChart data={activityStatistics} />
                            </Suspense>
                        </div>
                    ) : (
                        <EmptyState message="Brak danych aktywnosci do wyswietlenia wykresu." />
                    )}
                </section>

                <section className="dashboard-panel latest-imports-panel">
                    <div className="panel-heading">
                        <div>
                            <h3>Ostatnie importy</h3>
                            <p>Najnowsze pliki DDD w systemie.</p>
                        </div>
                        <Link to="/imports">Zobacz wszystkie</Link>
                    </div>

                    {dashboard.latestImports.length === 0 ? (
                        <EmptyState message="Brak zaimportowanych plikow DDD." />
                    ) : (
                        <div className="dashboard-table-wrapper">
                            <table className="dashboard-table">
                                <thead>
                                    <tr>
                                        <th>Plik</th>
                                        <th>Kierowca</th>
                                        <th>Data</th>
                                        <th>Aktywnosci</th>
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
                                                    [dddImport.driverFirstName, dddImport.driverLastName]
                                                        .filter(Boolean)
                                                        .join(" "),
                                                )}
                                            </td>
                                            <td>{formatDate(dddImport.uploadedAtUtc)}</td>
                                            <td>{dddImport.activitiesCount}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
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
    accent: "blue" | "cyan" | "amber" | "violet" | "green";
    compact?: boolean;
};

function SummaryCard({ label, value, accent, compact = false }: SummaryCardProps) {
    return (
        <article className={`summary-card ${accent}`}>
            <span>{label}</span>
            <strong className={compact ? "compact-value" : undefined}>{value}</strong>
        </article>
    );
}

function EmptyState({ message }: { message: string }) {
    return <p className="dashboard-empty">{message}</p>;
}

function DashboardSkeleton() {
    return (
        <div className="dashboard-page" aria-busy="true" aria-label="Ladowanie dashboardu">
            <div className="skeleton skeleton-heading" />
            <div className="dashboard-cards">
                {Array.from({ length: 5 }, (_, index) => (
                    <div className="skeleton skeleton-card" key={index} />
                ))}
            </div>
            <div className="skeleton dashboard-activity-skeleton" />
            <div className="skeleton dashboard-risk-skeleton" />
            <div className="analytics-grid">
                <div className="skeleton skeleton-panel" />
                <div className="skeleton skeleton-panel" />
            </div>
        </div>
    );
}
