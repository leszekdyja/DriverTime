import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import DashboardCharts, {
    type ActivityChartData,
    type ImportChartData,
    type ViolationChartData,
} from "../components/DashboardCharts";
import DriverRiskOverview from "../components/DriverRiskOverview";
import MetricCard from "../components/MetricCard";
import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    checkApiHealth,
    getDashboardData,
    type DashboardData,
    type DriverActivity,
} from "../services/dashboardService";
import {
    getDriverViolations,
    type DriverViolation,
} from "../services/violationsService";
import "../styles/dashboard.css";

const refreshIntervalMs = 30_000;

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const timeFormatter = new Intl.DateTimeFormat("pl-PL", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
});

const shortDateFormatter = new Intl.DateTimeFormat("pl-PL", {
    day: "2-digit",
    month: "2-digit",
});

const activityDefinitions = [
    { type: "DRIVING", label: "Jazda", tone: "driving", color: "var(--chart-driving)" },
    { type: "WORK", label: "Praca", tone: "work", color: "var(--chart-work)" },
    { type: "REST", label: "Odpoczynek", tone: "rest", color: "var(--chart-rest)" },
    { type: "AVAILABILITY", label: "Dyspozycja", tone: "availability", color: "var(--chart-availability)" },
] as const;

const violationSeverityDefinitions = [
    { severity: "minor", label: "Minor", color: "var(--chart-warning)" },
    { severity: "serious", label: "Serious", color: "var(--chart-orange)" },
    { severity: "very-serious", label: "Very serious", color: "var(--chart-danger)" },
] as const;

function formatDate(value: string | null) {
    if (!value) {
        return "Brak importów";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function formatTime(value: Date | null) {
    return value ? timeFormatter.format(value) : "Jeszcze nie odświeżono";
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(seconds, 0);
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    return hours === 0 ? `${minutes} min` : `${hours} godz. ${minutes} min`;
}

function displayValue(value: string) {
    return value || "Brak danych";
}

function getDriverName(firstName: string, lastName: string) {
    return [firstName, lastName].filter(Boolean).join(" ") || "Brak danych";
}

function normalizeSeverity(severity: string) {
    const value = severity.trim().toLowerCase();

    if (value === "high" || value === "severe" || value === "very serious" || value === "very-serious") {
        return "critical";
    }

    if (value === "medium" || value === "serious") {
        return "danger";
    }

    return "warning";
}

function normalizeViolationGroup(severity: string) {
    const value = severity.trim().toLowerCase();

    if (value === "high" || value === "severe" || value === "very serious" || value === "very-serious") {
        return "very-serious";
    }

    if (value === "medium" || value === "serious") {
        return "serious";
    }

    return "minor";
}

function toDayKey(date: Date) {
    const year = date.getFullYear();
    const month = `${date.getMonth() + 1}`.padStart(2, "0");
    const day = `${date.getDate()}`.padStart(2, "0");

    return `${year}-${month}-${day}`;
}

function isToday(value: string) {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return false;
    }

    return toDayKey(date) === toDayKey(new Date());
}

function isWithinLastDay(value: string) {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return false;
    }

    return Date.now() - date.getTime() <= 24 * 60 * 60 * 1000;
}

function buildImportChartData(dashboard: DashboardData): ImportChartData[] {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const days = Array.from({ length: 7 }, (_, index) => {
        const date = new Date(today);
        date.setDate(today.getDate() - (6 - index));

        return {
            day: toDayKey(date),
            label: shortDateFormatter.format(date),
            imports: 0,
        };
    });

    const indexByDay = new Map(days.map((item, index) => [item.day, index]));

    dashboard.imports.forEach((dddImport) => {
        const date = new Date(dddImport.uploadedAtUtc);

        if (Number.isNaN(date.getTime())) {
            return;
        }

        date.setHours(0, 0, 0, 0);
        const day = toDayKey(date);
        const index = indexByDay.get(day);

        if (index !== undefined) {
            days[index] = {
                ...days[index],
                imports: days[index].imports + 1,
            };
        }
    });

    return days;
}

function sumActivitiesDuration(activities: DriverActivity[]) {
    return activities.reduce(
        (total, activity) => total + Math.max(activity.durationSeconds, 0),
        0,
    );
}

export default function DashboardPage() {
    const [dashboard, setDashboard] = useState<DashboardData | null>(null);
    const [violations, setViolations] = useState<DriverViolation[]>([]);
    const [isInitialLoading, setIsInitialLoading] = useState(true);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [error, setError] = useState("");
    const [apiOnline, setApiOnline] = useState<boolean | null>(null);
    const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);

    const loadDashboard = useCallback(async (showInitialLoader = false) => {
        if (showInitialLoader) {
            setIsInitialLoading(true);
        } else {
            setIsRefreshing(true);
        }

        try {
            const [dashboardData, violationData, healthStatus] = await Promise.all([
                getDashboardData(),
                getDriverViolations(),
                checkApiHealth().catch(() => false),
            ]);

            setDashboard(dashboardData);
            setViolations(violationData);
            setApiOnline(healthStatus);
            setLastRefreshedAt(new Date());
            setError("");
        } catch (loadError) {
            setApiOnline(false);
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania dashboardu.",
            );
        } finally {
            setIsInitialLoading(false);
            setIsRefreshing(false);
        }
    }, []);

    useEffect(() => {
        void loadDashboard(true);

        const intervalId = window.setInterval(() => {
            void loadDashboard(false);
        }, refreshIntervalMs);

        return () => window.clearInterval(intervalId);
    }, [loadDashboard]);

    const activityStatistics = useMemo<ActivityChartData[]>(() => {
        return activityDefinitions.map((definition) => {
            const matchingActivities =
                dashboard?.activities.filter(
                    (activity) =>
                        activity.activityType.toUpperCase() === definition.type,
                ) ?? [];
            const duration = sumActivitiesDuration(matchingActivities);

            return {
                ...definition,
                count: matchingActivities.length,
                duration,
                hours: Number((duration / 3600).toFixed(2)),
            };
        });
    }, [dashboard]);

    const importChartData = useMemo<ImportChartData[]>(() => {
        return dashboard ? buildImportChartData(dashboard) : [];
    }, [dashboard]);

    const violationChartData = useMemo<ViolationChartData[]>(() => {
        return violationSeverityDefinitions.map((definition) => ({
            ...definition,
            count: violations.filter(
                (violation) => normalizeViolationGroup(violation.severity) === definition.severity,
            ).length,
        }));
    }, [violations]);

    const violationSummary = useMemo(() => {
        const severe = violations.filter((violation) =>
            normalizeSeverity(violation.severity) === "critical",
        ).length;
        const serious = violations.filter((violation) =>
            normalizeSeverity(violation.severity) === "danger",
        ).length;

        return {
            total: violations.length,
            severe,
            serious,
            latest: violations.slice(0, 4),
        };
    }, [violations]);

    const realtimeWidgets = useMemo(() => {
        const latestImport = dashboard?.latestImports[0] ?? null;
        const todayActivities = dashboard?.activities.filter((activity) => isToday(activity.startUtc)) ?? [];
        const latestViolations = violations.filter((violation) => isWithinLastDay(violation.occurredAtUtc));

        return {
            latestImport,
            todayActivities,
            todayActivityDuration: sumActivitiesDuration(todayActivities),
            latestViolations,
        };
    }, [dashboard, violations]);

    if (isInitialLoading && !dashboard) {
        return <DashboardSkeleton />;
    }

    if (!dashboard) {
        return (
            <div className="dashboard-page">
                <section className="dashboard-error-card" role="alert">
                    <strong>Nie można wyświetlić dashboardu</strong>
                    <p>{error || "Brak danych dashboardu."}</p>
                    <button className="dashboard-refresh-button" type="button" onClick={() => void loadDashboard(true)}>
                        Spróbuj ponownie
                    </button>
                </section>
            </div>
        );
    }

    return (
        <div className="dashboard-page">
            <section className="dashboard-hero">
                <div>
                    <span className="dashboard-eyebrow">DriverTime Command Center</span>
                    <h2>Dashboard floty i zgodności</h2>
                    <p>
                        Najważniejsze wskaźniki importów DDD, aktywności kierowców,
                        naruszeń i ryzyka operacyjnego w jednym miejscu.
                    </p>
                    <div className="dashboard-hero-actions">
                        <Link className="dashboard-primary-action" to="/imports">Dodaj import DDD</Link>
                        <Link className="dashboard-secondary-action" to="/reports">Przejdź do raportów</Link>
                    </div>
                </div>
                <aside className="dashboard-hero-card">
                    <span>Ostatni import</span>
                    <strong>{formatDate(dashboard.latestImportDate)}</strong>
                    <p>{dashboard.latestImports.length} ostatnich importów widocznych w historii</p>
                </aside>
            </section>

            <section className="dashboard-realtime-bar" aria-label="Odświeżanie dashboardu">
                <div>
                    <span className={`dashboard-live-dot ${apiOnline ? "online" : "offline"}`} />
                    <strong>{apiOnline ? "Dane online" : "API niedostępne"}</strong>
                    <span>Ostatnie odświeżenie: {formatTime(lastRefreshedAt)}</span>
                    {error ? <span className="dashboard-refresh-error">{error}</span> : null}
                </div>
                <button
                    className="dashboard-refresh-button"
                    type="button"
                    onClick={() => void loadDashboard(false)}
                    disabled={isRefreshing}
                >
                    {isRefreshing ? "Odświeżanie..." : "Odśwież"}
                </button>
            </section>

            <section className={`dashboard-realtime-grid${isRefreshing ? " is-refreshing" : ""}`} aria-label="Widgety realtime">
                <article className="dashboard-realtime-card">
                    <span>Ostatni import DDD</span>
                    {realtimeWidgets.latestImport ? (
                        <>
                            <strong>{realtimeWidgets.latestImport.fileName}</strong>
                            <p>{getDriverName(realtimeWidgets.latestImport.driverFirstName, realtimeWidgets.latestImport.driverLastName)}</p>
                            <small>{formatDate(realtimeWidgets.latestImport.uploadedAtUtc)}</small>
                        </>
                    ) : (
                        <p>Brak importów DDD.</p>
                    )}
                </article>

                <article className="dashboard-realtime-card warning">
                    <span>Nowe naruszenia</span>
                    <strong>{realtimeWidgets.latestViolations.length}</strong>
                    <p>Wykryte w ostatnich 24 godzinach</p>
                    <small>{violationSummary.severe} bardzo poważnych łącznie</small>
                </article>

                <article className="dashboard-realtime-card success">
                    <span>Aktywność dzisiaj</span>
                    <strong>{formatDuration(realtimeWidgets.todayActivityDuration)}</strong>
                    <p>{realtimeWidgets.todayActivities.length} zdarzeń z dzisiejszą datą</p>
                    <small>Liczone z obecnych aktywności</small>
                </article>

                <article className={`dashboard-realtime-card ${apiOnline ? "success" : "danger"}`}>
                    <span>Status API</span>
                    <strong>{apiOnline ? "Online" : "Offline"}</strong>
                    <p>{apiOnline ? "Endpoint health odpowiada poprawnie." : "Nie udało się potwierdzić statusu API."}</p>
                    <small>Autorefresh co 30 sekund</small>
                </article>
            </section>

            <section className="dashboard-kpi-grid" aria-label="Kluczowe wskaźniki">
                <MetricCard label="Importy" value={dashboard.totalImports} description="Pliki DDD w systemie" tone="blue" icon="DDD" />
                <MetricCard label="Kierowcy" value={dashboard.totalDrivers} description="Aktywne rekordy kierowców" tone="cyan" icon="DRV" />
                <MetricCard label="Aktywności" value={dashboard.totalActivities} description="Zdarzenia z kart kierowców" tone="violet" icon="ACT" />
                <MetricCard label="Pojazdy" value={dashboard.totalVehicles} description="Użycia pojazdów z DDD" tone="green" icon="TRK" />
                <MetricCard label="Naruszenia" value={violationSummary.total} description={`${violationSummary.severe} bardzo poważnych`} tone={violationSummary.severe > 0 ? "red" : "amber"} icon="!" />
            </section>

            <DashboardCharts
                activityData={activityStatistics}
                durationData={activityStatistics}
                importData={importChartData}
                violationData={violationChartData}
            />

            <section className="dashboard-premium-grid">
                <section className="dashboard-widget activity-widget">
                    <div className="dashboard-widget-heading">
                        <div>
                            <span>Aktywność kierowców</span>
                            <h3>Podsumowanie czasu</h3>
                        </div>
                    </div>
                    <div className="activity-metrics">
                        {activityStatistics.map((statistic) => (
                            <article className={`activity-metric ${statistic.tone}`} key={statistic.type}>
                                <span>{statistic.label}</span>
                                <strong>{formatDuration(statistic.duration)}</strong>
                                <small>{statistic.count} zdarzeń</small>
                            </article>
                        ))}
                    </div>
                </section>

                <section className="dashboard-widget violations-widget">
                    <div className="dashboard-widget-heading">
                        <div>
                            <span>Naruszenia</span>
                            <h3>Sygnały wymagające reakcji</h3>
                        </div>
                        <Link to="/violations">Zobacz wszystkie</Link>
                    </div>
                    <div className="violation-widget-summary">
                        <MetricCard label="Razem" value={violationSummary.total} tone="slate" description="W bieżących danych" />
                        <MetricCard label="Poważne" value={violationSummary.serious} tone="amber" description="Do sprawdzenia" />
                        <MetricCard label="Very serious" value={violationSummary.severe} tone="red" description="Wysoki priorytet" />
                    </div>
                    {violationSummary.latest.length === 0 ? (
                        <EmptyState
                            title="Brak naruszeń"
                            description="Aktualne dane nie zawierają naruszeń dla floty."
                        />
                    ) : (
                        <div className="dashboard-compact-list">
                            {violationSummary.latest.map((violation, index) => (
                                <article key={`${violation.driverCardNumber}-${violation.occurredAtUtc}-${index}`}>
                                    <div>
                                        <strong>{getDriverName(violation.driverFirstName, violation.driverLastName)}</strong>
                                        <span>{violation.violationType}</span>
                                    </div>
                                    <StatusBadge
                                        label={normalizeSeverity(violation.severity) === "critical" ? "Very serious" : "Serious"}
                                        tone={normalizeSeverity(violation.severity)}
                                    />
                                </article>
                            ))}
                        </div>
                    )}
                </section>
            </section>

            <section className="dashboard-widget latest-imports-widget">
                <div className="dashboard-widget-heading">
                    <div>
                        <span>Importy DDD</span>
                        <h3>Ostatnie pliki w systemie</h3>
                    </div>
                    <Link to="/imports">Historia importów</Link>
                </div>

                {dashboard.latestImports.length === 0 ? (
                    <EmptyState
                        title="Brak importów DDD"
                        description="Dodaj pierwszy plik DDD, aby rozpocząć analizę floty."
                    />
                ) : (
                    <div className="dashboard-table-wrapper">
                        <table className="dashboard-table premium-table">
                            <thead>
                                <tr>
                                    <th>Plik</th>
                                    <th>Kierowca</th>
                                    <th>Data importu</th>
                                    <th>Aktywności</th>
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

            <DriverRiskOverview />
        </div>
    );
}

function DashboardSkeleton() {
    return (
        <div className="dashboard-page" aria-busy="true" aria-label="Ładowanie dashboardu">
            <div className="skeleton skeleton-heading" />
            <div className="dashboard-kpi-grid">
                {Array.from({ length: 5 }, (_, index) => (
                    <div className="skeleton skeleton-card" key={index} />
                ))}
            </div>
            <div className="dashboard-charts-grid">
                {Array.from({ length: 4 }, (_, index) => (
                    <div className="skeleton skeleton-panel" key={index} />
                ))}
            </div>
            <div className="dashboard-premium-grid">
                <div className="skeleton skeleton-panel" />
                <div className="skeleton skeleton-panel" />
            </div>
            <div className="dashboard-widget">
                <TableSkeleton rows={5} columns={4} />
            </div>
        </div>
    );
}
