import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getAlerts,
    type AlertCategory,
    type AlertItem,
    type AlertSeverity,
} from "../services/alertsService";
import {
    getAlertCategoryLabel,
    getComplianceRuleLabel,
    getSeverityLabel,
} from "../utils/complianceLabels";
import "../styles/dashboard.css";
import "../styles/drivers.css";

type Filter = "All" | AlertCategory | AlertSeverity;

const filters: Array<{ value: Filter; label: string }> = [
    { value: "All", label: "Wszystkie" },
    { value: "Compliance", label: "Zgodność" },
    { value: "Downloads", label: "Odczyty" },
    { value: "Imports", label: "Importy" },
    { value: "Critical", label: "Krytyczne" },
    { value: "Warning", label: "Ostrzeżenia" },
];

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) {
        return "Brak terminu";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function categoryLabel(category: AlertCategory) {
    return getAlertCategoryLabel(category);
}

function severityLabel(severity: AlertSeverity) {
    return getSeverityLabel(severity);
}

function alertDescription(alert: AlertItem) {
    if (alert.type === "ComplianceViolation") {
        return getComplianceRuleLabel(alert.description);
    }

    return alert.description || "Brak opisu";
}

function severityTone(severity: AlertSeverity) {
    if (severity === "Critical") return "critical";
    if (severity === "Warning") return "warning";
    return "info";
}

function filterAlerts(alerts: AlertItem[], filter: Filter) {
    if (filter === "All") {
        return alerts;
    }

    return alerts.filter((alert) =>
        alert.category === filter || alert.severity === filter);
}

export default function AlertsPage() {
    const [alerts, setAlerts] = useState<AlertItem[]>([]);
    const [activeFilter, setActiveFilter] = useState<Filter>("All");
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    const loadAlerts = useCallback(async () => {
        setIsLoading(true);
        setError("");

        try {
            setAlerts(await getAlerts());
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania alertów.",
            );
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        void loadAlerts();
    }, [loadAlerts]);

    const filteredAlerts = useMemo(
        () => filterAlerts(alerts, activeFilter),
        [activeFilter, alerts],
    );

    const criticalCount = useMemo(
        () => alerts.filter((alert) => alert.severity === "Critical").length,
        [alerts],
    );

    const warningCount = useMemo(
        () => alerts.filter((alert) => alert.severity === "Warning").length,
        [alerts],
    );

    return (
        <div className="drivers-page">
            <div className="drivers-heading">
                <div>
                    <h2>Alerty</h2>
                    <p>Centrum alertów z compliance, terminów odczytów i monitoringu importów.</p>
                </div>
                <span className="drivers-count">{filteredAlerts.length} alertów</span>
            </div>

            <section className="dashboard-kpi-grid" aria-label="Podsumowanie alertów" style={{ marginTop: 28 }}>
                <article className="metric-card red">
                    <div className="metric-card-heading"><span>Krytyczne</span></div>
                    <strong>{criticalCount}</strong>
                    <small>Wymagają pilnej reakcji</small>
                </article>
                <article className="metric-card amber">
                    <div className="metric-card-heading"><span>Ostrzeżenia</span></div>
                    <strong>{warningCount}</strong>
                    <small>Do obsługi w najbliższym czasie</small>
                </article>
                <article className="metric-card slate">
                    <div className="metric-card-heading"><span>Razem</span></div>
                    <strong>{alerts.length}</strong>
                    <small>Otwarte alerty</small>
                </article>
            </section>

            <section className="drivers-panel" style={{ marginTop: 28 }}>
                <div className="section-heading">
                    <h3>Lista alertów</h3>
                    <p>Alerty są wyliczane dynamicznie z istniejących danych systemu.</p>
                </div>

                <div className="drivers-toolbar" aria-label="Filtry alertów">
                    {filters.map((filter) => (
                        <button
                            key={filter.value}
                            type="button"
                            onClick={() => setActiveFilter(filter.value)}
                            aria-pressed={activeFilter === filter.value}
                        >
                            {filter.label}
                        </button>
                    ))}
                </div>

                {error ? (
                    <p className="drivers-message error" role="alert">
                        {error}
                    </p>
                ) : null}

                {isLoading ? (
                    <TableSkeleton rows={7} columns={7} />
                ) : filteredAlerts.length === 0 && !error ? (
                    <EmptyState
                        title="Brak alertów"
                        description="Dla wybranego filtra nie ma obecnie otwartych alertów."
                    />
                ) : null}

                {!isLoading && filteredAlerts.length > 0 ? (
                    <div className="drivers-table-wrapper">
                        <table className="drivers-table">
                            <thead>
                                <tr>
                                    <th>Priorytet</th>
                                    <th>Kategoria</th>
                                    <th>Tytuł</th>
                                    <th>Opis</th>
                                    <th>Obiekt powiązany</th>
                                    <th>Termin/data</th>
                                    <th>Akcja</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredAlerts.map((alert) => (
                                    <tr key={alert.id}>
                                        <td>
                                            <StatusBadge
                                                label={severityLabel(alert.severity)}
                                                tone={severityTone(alert.severity)}
                                            />
                                        </td>
                                        <td>{categoryLabel(alert.category)}</td>
                                        <td>{alert.title}</td>
                                        <td>{alertDescription(alert)}</td>
                                        <td>{alert.relatedEntityName || alert.relatedEntityType || "Brak danych"}</td>
                                        <td>{formatDate(alert.dueDateUtc ?? alert.createdAtUtc)}</td>
                                        <td>
                                            {alert.actionUrl ? (
                                                <Link className="driver-details-link" to={alert.actionUrl}>
                                                    Przejdź
                                                </Link>
                                            ) : (
                                                "Brak"
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : null}
            </section>
        </div>
    );
}
