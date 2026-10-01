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
    getComplianceRunDashboardStats,
    getDashboardData,
    getDashboardDrivers,
    getDownloadDashboard,
    type ComplianceRunDashboardStats,
    type DashboardData,
    type DashboardDriver,
    type DashboardViolation,
} from "../services/dashboardService";
import {
    getDriverDownloads,
    getVehicleDownloads,
    type DownloadDashboard,
    type DriverDownload,
    type VehicleDownload,
} from "../services/downloadsService";
import { getComplianceRuleLabel, getSeverityLabel } from "../utils/complianceLabels";
import { formatDriverNameOrFallback } from "../utils/driverName";
import "../styles/dashboard.css";

const maxOperationalAlerts = 8;

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
    { type: "AVAILABILITY", label: "Dyspozycyjność", tone: "availability", color: "var(--chart-availability)" },
] as const;

type OperationalAlertPriority = "High" | "Medium" | "Low";

type OperationalAlert = {
    id: string;
    type: string;
    title: string;
    description: string;
    priority: OperationalAlertPriority;
    dueDateUtc: string | null;
    actionUrl: string;
};

const violationSeverityDefinitions = [
    { severity: "info", label: "Info", color: "var(--chart-warning)" },
    { severity: "warning", label: "Ostrzeżenie", color: "var(--chart-orange)" },
    { severity: "critical", label: "Krytyczne", color: "var(--chart-danger)" },
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

function formatOptionalDate(value: string | null) {
    if (!value) {
        return "Brak danych";
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
    return formatDriverNameOrFallback(firstName, lastName);
}

function getViolationType(violation: DashboardViolation) {
    return getComplianceRuleLabel(violation.violationType, violation.code);
}

function normalizeSeverity(severity: string) {
    const value = severity.trim().toLowerCase();

    if (value === "critical" || value === "high" || value === "severe" || value === "very serious" || value === "very-serious") {
        return "critical";
    }

    if (value === "warning" || value === "medium" || value === "serious") {
        return "danger";
    }

    return "warning";
}

function normalizeViolationGroup(severity: string) {
    const value = severity.trim().toLowerCase();

    if (value === "critical" || value === "high" || value === "severe" || value === "very serious" || value === "very-serious") {
        return "critical";
    }

    if (value === "warning" || value === "medium" || value === "serious") {
        return "warning";
    }

    return "info";
}

function isHighSeverity(severity: string) {
    return normalizeViolationGroup(severity) === "critical";
}

function formatRunStatus(status: string) {
    const normalized = status.trim().toLowerCase();

    if (normalized === "completed") {
        return "Zakończono";
    }

    if (normalized === "running" || normalized === "processing") {
        return "W trakcie";
    }

    if (normalized === "pending") {
        return "Oczekuje";
    }

    if (normalized === "failed") {
        return "Niepowodzenie";
    }

    if (normalized === "queued") {
        return "W kolejce";
    }

    if (normalized === "retrying") {
        return "Ponawianie";
    }

    return "Brak danych";
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

function getDaysUntil(value: string) {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return null;
    }

    const now = new Date();
    const todayUtc = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
    const targetUtc = Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());

    return Math.ceil((targetUtc - todayUtc) / (24 * 60 * 60 * 1000));
}

function formatAlertDueDate(value: string | null) {
    return value ? formatOptionalDate(value) : "Brak terminu";
}

function getPriorityTone(priority: OperationalAlertPriority) {
    if (priority === "High") return "danger";
    if (priority === "Medium") return "warning";
    return "neutral";
}

function getPriorityLabel(priority: OperationalAlertPriority) {
    if (priority === "High") return "Wysoki";
    if (priority === "Medium") return "Średni";
    return "Niski";
}

function priorityRank(priority: OperationalAlertPriority) {
    if (priority === "High") return 0;
    if (priority === "Medium") return 1;
    return 2;
}

function getDriverDisplayName(driver: Pick<DashboardDriver, "firstName" | "lastName" | "cardNumber">) {
    return getDriverName(driver.firstName, driver.lastName) || driver.cardNumber || "Kierowca";
}

function buildImportChartData(dashboard: DashboardData): ImportChartData[] {
    return dashboard.importTrend.map((item) => {
        const date = new Date(item.dayUtc);

        return {
            day: Number.isNaN(date.getTime()) ? item.dayUtc : toDayKey(date),
            label: Number.isNaN(date.getTime()) ? item.dayUtc : shortDateFormatter.format(date),
            imports: item.importsCount,
        };
    });
}

export default function DashboardPage() {
    const [dashboard, setDashboard] = useState<DashboardData | null>(null);
    const [violations, setViolations] = useState<DashboardViolation[]>([]);
    const [complianceStats, setComplianceStats] = useState<ComplianceRunDashboardStats | null>(null);
    const [downloadStats, setDownloadStats] = useState<DownloadDashboard | null>(null);
    const [drivers, setDrivers] = useState<DashboardDriver[]>([]);
    const [driverDownloads, setDriverDownloads] = useState<DriverDownload[]>([]);
    const [vehicleDownloads, setVehicleDownloads] = useState<VehicleDownload[]>([]);
    const [isInitialLoading, setIsInitialLoading] = useState(true);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [isComplianceStatsLoading, setIsComplianceStatsLoading] = useState(true);
    const [isDownloadStatsLoading, setIsDownloadStatsLoading] = useState(true);
    const [error, setError] = useState("");
    const [complianceStatsError, setComplianceStatsError] = useState("");
    const [downloadStatsError, setDownloadStatsError] = useState("");
    const [apiOnline, setApiOnline] = useState<boolean | null>(null);
    const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);

    const loadDashboard = useCallback(async (showInitialLoader = false) => {
        if (showInitialLoader) {
            setIsInitialLoading(true);
        } else {
            setIsRefreshing(true);
        }

        setIsComplianceStatsLoading(true);
        setIsDownloadStatsLoading(true);

        try {
            const [
                dashboardData,
                healthStatus,
                complianceResult,
                downloadResult,
                driversResult,
                driverDownloadsResult,
                vehicleDownloadsResult,
            ] = await Promise.all([
                getDashboardData(),
                checkApiHealth().catch(() => false),
                getComplianceRunDashboardStats()
                    .then((data) => ({ data, error: "" }))
                    .catch((statsError) => ({
                        data: null,
                        error: statsError instanceof Error
                            ? statsError.message
                            : "Wystąpił błąd podczas pobierania statystyk compliance.",
                    })),
                getDownloadDashboard()
                    .then((data) => ({ data, error: "" }))
                    .catch((statsError) => ({
                        data: null,
                        error: statsError instanceof Error
                            ? statsError.message
                            : "Wystąpił błąd podczas pobierania terminów odczytów.",
                    })),
                getDashboardDrivers().catch(() => []),
                getDriverDownloads().catch(() => []),
                getVehicleDownloads().catch(() => []),
            ]);

            setDashboard(dashboardData);
            setViolations(dashboardData.latestViolations);
            setApiOnline(healthStatus);
            setComplianceStats(complianceResult.data);
            setComplianceStatsError(complianceResult.error);
            setDownloadStats(downloadResult.data);
            setDownloadStatsError(downloadResult.error);
            setDrivers(driversResult);
            setDriverDownloads(driverDownloadsResult);
            setVehicleDownloads(vehicleDownloadsResult);
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
            setIsComplianceStatsLoading(false);
            setIsDownloadStatsLoading(false);
        }
    }, []);

    useEffect(() => {
        void loadDashboard(true);
    }, [loadDashboard]);

    const activityStatistics = useMemo<ActivityChartData[]>((() => {
        return activityDefinitions.map((definition) => {
            const summary = dashboard?.activitySummaries.find(
                (activity) => activity.activityType.toUpperCase() === definition.type,
            );
            const duration = Math.max(summary?.durationSeconds ?? 0, 0);

            return {
                ...definition,
                count: summary?.count ?? 0,
                duration,
                hours: Number((duration / 3600).toFixed(2)),
            };
        });
    }), [dashboard]);

    const importChartData = useMemo<ImportChartData[]>(() => {
        return dashboard ? buildImportChartData(dashboard) : [];
    }, [dashboard]);

    const violationChartData = useMemo<ViolationChartData[]>(() => {
        return violationSeverityDefinitions.map((definition) => ({
            ...definition,
            count: dashboard?.violationSummaries
                .filter((violation) => normalizeViolationGroup(violation.severity) === definition.severity)
                .reduce((total, violation) => total + violation.count, 0) ?? 0,
        }));
    }, [dashboard]);

    const violationSummary = useMemo(() => {
        const severe = dashboard?.violationSummaries
            .filter((violation) => normalizeSeverity(violation.severity) === "critical")
            .reduce((total, violation) => total + violation.count, 0) ?? 0;
        const serious = dashboard?.violationSummaries
            .filter((violation) => normalizeSeverity(violation.severity) === "danger")
            .reduce((total, violation) => total + violation.count, 0) ?? 0;

        return {
            total: dashboard?.totalViolations ?? 0,
            severe,
            serious,
            today: violations.filter((violation) => isToday(violation.occurredAtUtc)).length,
            latestCritical: violations
                .filter((violation) => normalizeSeverity(violation.severity) === "critical")
                .slice(0, 4),
            latest: violations.slice(0, 4),
        };
    }, [dashboard, violations]);

    const operationalAlerts = useMemo<OperationalAlert[]>(() => {
        const alerts: OperationalAlert[] = [];

        driverDownloads.forEach((download) => {
            if (download.status !== "Overdue" && download.status !== "Warning") {
                return;
            }

            const driverName = getDriverDisplayName(download);
            const isOverdue = download.status === "Overdue";

            alerts.push({
                id: `driver-download-${download.driverId}-${download.nextRequiredDownloadUtc ?? "missing"}`,
                type: "Odczyt kierowcy",
                title: isOverdue
                    ? `Odczyt karty po terminie: ${driverName}`
                    : `Zbliża się termin odczytu karty: ${driverName}`,
                description: isOverdue
                    ? "Karta kierowcy przekroczyła wymagany cykl odczytu 28 dni."
                    : "Termin odczytu karty kierowcy przypada w ciągu 7 dni.",
                priority: isOverdue ? "High" : "Medium",
                dueDateUtc: download.nextRequiredDownloadUtc,
                actionUrl: "/downloads",
            });
        });

        vehicleDownloads.forEach((download) => {
            if (download.status !== "Overdue" && download.status !== "Warning") {
                return;
            }

            const isOverdue = download.status === "Overdue";

            alerts.push({
                id: `vehicle-download-${download.vehicleId}-${download.nextRequiredDownloadUtc ?? "missing"}`,
                type: "Odczyt pojazdu",
                title: isOverdue
                    ? `Odczyt tachografu po terminie: ${download.registrationNumber}`
                    : `Zbliża się termin odczytu tachografu: ${download.registrationNumber}`,
                description: isOverdue
                    ? "Pojazd przekroczył wymagany cykl odczytu tachografu 90 dni."
                    : "Termin odczytu pojazdu lub tachografu przypada w ciągu 7 dni.",
                priority: isOverdue ? "High" : "Medium",
                dueDateUtc: download.nextRequiredDownloadUtc,
                actionUrl: "/downloads",
            });
        });

        drivers.forEach((driver) => {
            if (!driver.cardExpiryDate) {
                return;
            }

            const daysUntilExpiry = getDaysUntil(driver.cardExpiryDate);
            if (daysUntilExpiry === null || daysUntilExpiry > 30) {
                return;
            }

            const driverName = getDriverDisplayName(driver);
            const isExpired = daysUntilExpiry < 0;

            alerts.push({
                id: `driver-card-${driver.id}-${driver.cardExpiryDate}`,
                type: "Karta kierowcy",
                title: isExpired
                    ? `Karta kierowcy wygasła: ${driverName}`
                    : `Karta kierowcy wygasa: ${driverName}`,
                description: isExpired
                    ? "Karta kierowcy jest po terminie ważności."
                    : `Karta kierowcy wygaśnie za ${daysUntilExpiry} dni.`,
                priority: isExpired ? "High" : "Medium",
                dueDateUtc: driver.cardExpiryDate,
                actionUrl: `/drivers/${driver.id}`,
            });
        });

        violations
            .filter((violation) => isHighSeverity(violation.severity))
            .slice(0, 12)
            .forEach((violation) => {
                const driverName = getDriverName(violation.driverFirstName, violation.driverLastName);

                alerts.push({
                    id: `high-violation-${violation.id}`,
                    type: "Naruszenie",
                    title: `Naruszenie krytyczne: ${driverName}`,
                    description: getViolationType(violation) || violation.description || "Wysokie naruszenie zgodności wymaga sprawdzenia.",
                    priority: "High",
                    dueDateUtc: violation.occurredAtUtc,
                    actionUrl: violation.driverId
                        ? `/violations?driverId=${violation.driverId}&violationId=${violation.id}`
                        : "/violations",
                });
            });

        return alerts
            .sort((left, right) => {
                const priorityDifference = priorityRank(left.priority) - priorityRank(right.priority);
                if (priorityDifference !== 0) {
                    return priorityDifference;
                }

                const leftTime = left.dueDateUtc ? new Date(left.dueDateUtc).getTime() : Number.MAX_SAFE_INTEGER;
                const rightTime = right.dueDateUtc ? new Date(right.dueDateUtc).getTime() : Number.MAX_SAFE_INTEGER;

                return leftTime - rightTime;
            })
            .slice(0, maxOperationalAlerts);
    }, [driverDownloads, drivers, vehicleDownloads, violations]);

    const realtimeWidgets = useMemo(() => {
        const latestImport = dashboard?.latestImports[0] ?? null;
        const activityCount = dashboard?.activitySummaries.reduce(
            (total, activity) => total + Math.max(activity.count, 0),
            0,
        ) ?? 0;
        const activityDuration = dashboard?.activitySummaries.reduce(
            (total, activity) => total + Math.max(activity.durationSeconds, 0),
            0,
        ) ?? 0;
        const latestViolations = violations.filter((violation) => isWithinLastDay(violation.occurredAtUtc));

        return {
            latestImport,
            activityCount,
            activityDuration,
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
                    <span className="dashboard-eyebrow">Centrum operacyjne DriverTime</span>
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
                    <strong>{formatDuration(realtimeWidgets.activityDuration)}</strong>
                    <p>{realtimeWidgets.activityCount} zdarzeń w zakresie dashboardu</p>
                    <small>Liczone z obecnych aktywności</small>
                </article>

                <article className={`dashboard-realtime-card ${apiOnline ? "success" : "danger"}`}>
                    <span>Status API</span>
                    <strong>{apiOnline ? "Online" : "Offline"}</strong>
                    <p>{apiOnline ? "Endpoint health odpowiada poprawnie." : "Nie udało się potwierdzić statusu API."}</p>
                    <small>Autorefresh co 30 sekund</small>
                </article>
            </section>

            <section className="dashboard-widget operational-alerts-widget">
                <div className="dashboard-widget-heading">
                    <div>
                        <span>Alerty operacyjne</span>
                        <h3>Pilne sprawy do sprawdzenia</h3>
                        <p>Najważniejsze sygnały z odczytów, kart kierowców i naruszeń krytycznych.</p>
                    </div>
                    <Link to="/alerts">Zobacz wszystkie alerty</Link>
                </div>

                {operationalAlerts.length === 0 ? (
                    <EmptyState
                        title="Brak pilnych alertów operacyjnych."
                        description="Nie znaleziono przeterminowanych odczytów, wygasających kart ani naruszeń krytycznych."
                    />
                ) : (
                    <div className="operational-alerts-list">
                        {operationalAlerts.map((alert) => (
                            <article className={`operational-alert-card ${alert.priority.toLowerCase()}`} key={alert.id}>
                                <div className="operational-alert-main">
                                    <div className="operational-alert-title-row">
                                        <StatusBadge
                                            label={getPriorityLabel(alert.priority)}
                                            tone={getPriorityTone(alert.priority)}
                                        />
                                        <span>{alert.type}</span>
                                    </div>
                                    <h4>{alert.title}</h4>
                                    <p>{alert.description}</p>
                                </div>
                                <div className="operational-alert-meta">
                                    <span>{formatAlertDueDate(alert.dueDateUtc)}</span>
                                    <Link to={alert.actionUrl}>Przejdź</Link>
                                </div>
                            </article>
                        ))}
                    </div>
                )}
            </section>

            <section className="dashboard-kpi-grid" aria-label="Kluczowe wskaźniki">
                <MetricCard label="Importy" value={dashboard.totalImports} description="Pliki DDD w systemie" tone="blue" icon="DDD" />
                <MetricCard label="Kierowcy" value={dashboard.totalDrivers} description="Aktywne rekordy kierowców" tone="cyan" icon="DRV" />
                <MetricCard label="Aktywności" value={dashboard.totalActivities} description="Zdarzenia z kart kierowców" tone="violet" icon="ACT" />
                <MetricCard label="Pojazdy" value={dashboard.totalVehicles} description="Użycia pojazdów z DDD" tone="green" icon="TRK" />
                <MetricCard label="Naruszenia" value={violationSummary.total} description={`${violationSummary.severe} bardzo poważnych`} tone={violationSummary.severe > 0 ? "red" : "amber"} icon="!" />
                <MetricCard label="Naruszenia dziś" value={violationSummary.today} description="Wykryte dla bieżącej daty" tone={violationSummary.today > 0 ? "red" : "green"} icon="24H" />
            </section>

            <section className="dashboard-widget latest-imports-widget">
                <div className="dashboard-widget-heading">
                    <div>
                        <span>Alerty</span>
                        <h3>Najważniejsze sygnały operacyjne</h3>
                        <p>Alerty są liczone z terminów pobrań oraz zapisanych naruszeń zgodności.</p>
                    </div>
                    <Link to="/alerts">Otwórz centrum alertów</Link>
                </div>

                <div className="violation-widget-summary">
                    <MetricCard
                        label="Kierowcy po terminie"
                        value={dashboard.alerts.overdueDriverDownloads}
                        tone={dashboard.alerts.overdueDriverDownloads > 0 ? "red" : "green"}
                        description="Odczyt karty powyżej 28 dni"
                    />
                    <MetricCard
                        label="Kierowcy do 7 dni"
                        value={dashboard.alerts.driverDownloadsDueIn7Days}
                        tone={dashboard.alerts.driverDownloadsDueIn7Days > 0 ? "amber" : "green"}
                        description="Pilne pobrania kart"
                    />
                    <MetricCard
                        label="Odczyty do 7 dni"
                        value={dashboard.alerts.downloadsDueIn7Days}
                        tone={dashboard.alerts.downloadsDueIn7Days > 0 ? "amber" : "green"}
                        description="Karty i pojazdy razem"
                    />
                    <MetricCard
                        label="Kierowcy do 14 dni"
                        value={dashboard.alerts.driverDownloadsDueIn14Days}
                        tone={dashboard.alerts.driverDownloadsDueIn14Days > 0 ? "amber" : "green"}
                        description="Nadchodzące pobrania kart"
                    />
                    <MetricCard
                        label="Pojazdy po terminie"
                        value={dashboard.alerts.overdueVehicleDownloads}
                        tone={dashboard.alerts.overdueVehicleDownloads > 0 ? "red" : "green"}
                        description="Tachograf lub pojazd powyżej 90 dni"
                    />
                    <MetricCard
                        label="Pojazdy do 7 dni"
                        value={dashboard.alerts.vehicleDownloadsDueIn7Days}
                        tone={dashboard.alerts.vehicleDownloadsDueIn7Days > 0 ? "amber" : "green"}
                        description="Pilne pobrania tachografów"
                    />
                    <MetricCard
                        label="Pojazdy do 14 dni"
                        value={dashboard.alerts.vehicleDownloadsDueIn14Days}
                        tone={dashboard.alerts.vehicleDownloadsDueIn14Days > 0 ? "amber" : "green"}
                        description="Nadchodzące pobrania tachografów"
                    />
                    <MetricCard
                        label="Krytyczne zgodności"
                        value={dashboard.alerts.driversWithHighViolations}
                        tone={dashboard.alerts.driversWithHighViolations > 0 ? "red" : "green"}
                        description="Kierowcy z wysokimi naruszeniami"
                    />
                    <MetricCard
                        label="Ostrzeżenia zgodności"
                        value={dashboard.alerts.driversWithMediumViolations}
                        tone={dashboard.alerts.driversWithMediumViolations > 0 ? "amber" : "green"}
                        description="Kierowcy ze średnimi naruszeniami"
                    />
                </div>
            </section>

            <section className="dashboard-widget latest-imports-widget">
                <div className="dashboard-widget-heading">
                    <div>
                        <span>Terminy odczytów</span>
                        <h3>Terminy pobrań z kart i tachografów</h3>
                        <p>Karty kierowców: 28 dni. Tachografy i pojazdy: 90 dni.</p>
                    </div>
                    <Link to="/downloads">Zobacz terminy</Link>
                </div>

                {isDownloadStatsLoading ? (
                    <TableSkeleton rows={2} columns={4} />
                ) : downloadStatsError ? (
                    <p className="driver-risk-error" role="alert">{downloadStatsError}</p>
                ) : !downloadStats ? (
                    <EmptyState
                        title="Brak danych odczytów"
                        description="Dane pojawią się po dodaniu kierowców, pojazdów i importów DDD."
                    />
                ) : (
                    <div className="violation-widget-summary">
                        <MetricCard label="Kierowcy po terminie" value={downloadStats.overdueDrivers} tone={downloadStats.overdueDrivers > 0 ? "red" : "green"} description="Karta kierowcy powyżej 28 dni" />
                        <MetricCard label="Kierowcy do 7 dni" value={downloadStats.warningDrivers} tone={downloadStats.warningDrivers > 0 ? "amber" : "green"} description="Zbliżający się termin" />
                        <MetricCard label="Pojazdy po terminie" value={downloadStats.overdueVehicles} tone={downloadStats.overdueVehicles > 0 ? "red" : "green"} description="Tachograf lub pojazd powyżej 90 dni" />
                        <MetricCard label="Pojazdy do 7 dni" value={downloadStats.warningVehicles} tone={downloadStats.warningVehicles > 0 ? "amber" : "green"} description="Zbliżający się termin" />
                    </div>
                )}
            </section>

            {(isComplianceStatsLoading || complianceStatsError || (complianceStats && complianceStats.recentRunsCount > 0)) && (
            <section className="dashboard-widget latest-imports-widget">
                <div className="dashboard-widget-heading">
                    <div>
                        <span>Ocena zgodności</span>
                        <h3>Historia automatycznej oceny zgodności</h3>
                        <p>Dane pochodzą z zapisanych wyników oceny zgodności, bez ponownego liczenia podglądu na dashboardzie.</p>
                    </div>
                </div>

                {isComplianceStatsLoading ? (
                    <TableSkeleton rows={3} columns={4} />
                ) : complianceStatsError ? (
                    <p className="driver-risk-error" role="alert">{complianceStatsError}</p>
                ) : complianceStats ? (
                    <>
                        <div className="violation-widget-summary">
                            <MetricCard label="Ostatnie oceny" value={complianceStats.recentRunsCount} tone="slate" description="Ostatnie zapisane wyniki" />
                            <MetricCard label="Status" value={formatRunStatus(complianceStats.lastStatus)} tone={complianceStats.lastStatus === "Completed" ? "green" : "amber"} description={formatOptionalDate(complianceStats.lastRunAtUtc)} />
                            <MetricCard label="Naruszenia" value={complianceStats.lastRunViolationsCount} tone={complianceStats.lastRunViolationsCount > 0 ? "red" : "green"} description="Z ostatniej oceny" />
                        </div>
                        <div className="activity-metrics">
                            <article className="activity-metric driving">
                                <span>Krytyczne</span>
                                <strong>{complianceStats.highViolationsCount}</strong>
                                <small>Naruszenia wysokiego priorytetu</small>
                            </article>
                            <article className="activity-metric work">
                                <span>Ostrzeżenia</span>
                                <strong>{complianceStats.mediumViolationsCount}</strong>
                                <small>Naruszenia średniego priorytetu</small>
                            </article>
                            <article className="activity-metric rest">
                                <span>Niskie</span>
                                <strong>{complianceStats.lowViolationsCount}</strong>
                                <small>Informacyjne wyniki zgodności</small>
                            </article>
                            <article className="activity-metric availability">
                                <span>Automat</span>
                                <strong>{complianceStats.schedulerEnabled ? "Włączony" : "Wyłączony"}</strong>
                                <small>Ostatnie automatyczne uruchomienie: {formatOptionalDate(complianceStats.lastSchedulerRunAtUtc)}</small>
                            </article>
                        </div>
                        <div className="risk-quick-stats">
                            <span><strong>{complianceStats.driversInLastRunCount}</strong> kierowców w ostatnim cyklu</span>
                            <span>Status automatu: <strong>{formatRunStatus(complianceStats.lastSchedulerStatus)}</strong></span>
                            <span>Automatyczne naruszenia: <strong>{complianceStats.lastSchedulerViolationsCount}</strong></span>
                        </div>
                    </>
                ) : null}
            </section>
            )}

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
                        <MetricCard label="Krytyczne" value={violationSummary.severe} tone="red" description="Wysoki priorytet" />
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
                                        <span>{getViolationType(violation)}</span>
                                    </div>
                                    <StatusBadge
                                        label={getSeverityLabel(violation.severity)}
                                        tone={normalizeSeverity(violation.severity)}
                                    />
                                </article>
                            ))}
                        </div>
                    )}
                    {violationSummary.latestCritical.length > 0 && (
                        <div className="dashboard-critical-list">
                            <strong>Ostatnie krytyczne naruszenia</strong>
                            {violationSummary.latestCritical.map((violation, index) => (
                                <article key={`${violation.id}-${index}`}>
                                    <span>{getDriverName(violation.driverFirstName, violation.driverLastName)}</span>
                                    <small>{getViolationType(violation)}</small>
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
                                                getDriverName(dddImport.driverFirstName, dddImport.driverLastName),
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
