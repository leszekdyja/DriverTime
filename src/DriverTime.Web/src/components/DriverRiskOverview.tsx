import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import {
    getDriverRiskOverview,
    type DriverRisk,
    type DriverRiskOverview as DriverRiskOverviewData,
} from "../services/dashboardService";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) return "Brak importu";

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDays(value: number | null) {
    return value === null ? "Brak danych" : `${value} dni`;
}

function getRiskLabel(status: DriverRisk["riskStatus"]) {
    return {
        Low: "Niskie",
        Medium: "Srednie",
        High: "Wysokie",
        Critical: "Krytyczne",
    }[status];
}

export default function DriverRiskOverview() {
    const [overview, setOverview] = useState<DriverRiskOverviewData | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        async function loadRiskOverview() {
            try {
                setOverview(await getDriverRiskOverview());
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystapil blad podczas pobierania ryzyka kierowcow.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadRiskOverview();
    }, []);

    return (
        <section className="driver-risk-section">
            <div className="panel-heading">
                <div>
                    <h3>Ryzyko kierowcow</h3>
                    <p>Alerty na podstawie naruszen i aktualnosci danych DDD.</p>
                </div>
            </div>

            {isLoading ? (
                <div className="driver-risk-loading" aria-label="Ladowanie ryzyka">
                    {Array.from({ length: 4 }, (_, index) => (
                        <div className="skeleton risk-skeleton-card" key={index} />
                    ))}
                </div>
            ) : error ? (
                <p className="driver-risk-error" role="alert">{error}</p>
            ) : overview ? (
                <>
                    <div className="driver-risk-cards">
                        <RiskCard label="Niskie" count={overview.lowRiskCount} status="low" />
                        <RiskCard label="Srednie" count={overview.mediumRiskCount} status="medium" />
                        <RiskCard label="Wysokie" count={overview.highRiskCount} status="high" />
                        <RiskCard label="Krytyczne" count={overview.criticalRiskCount} status="critical" />
                    </div>

                    {overview.drivers.length === 0 ? (
                        <p className="dashboard-empty">Brak kierowcow do analizy ryzyka.</p>
                    ) : (
                        <div className="dashboard-table-wrapper driver-risk-table-wrapper">
                            <table className="dashboard-table driver-risk-table">
                                <thead><tr><th>Kierowca</th><th>Karta</th><th>Status</th><th>Naruszenia</th><th>Ciezkie</th><th>Ostatni import</th><th>Bez aktywnosci</th><th>Bez importu</th></tr></thead>
                                <tbody>{overview.drivers.map((driver) => <tr key={driver.driverId}><td><Link to={`/drivers/${driver.driverId}`}>{`${driver.firstName} ${driver.lastName}`.trim() || "Brak danych"}</Link></td><td>{driver.cardNumber || "Brak danych"}</td><td><span className={`risk-badge ${driver.riskStatus.toLowerCase()}`}>{getRiskLabel(driver.riskStatus)}</span></td><td>{driver.violationsCount}</td><td><strong className={driver.severeViolationsCount > 0 ? "severe-count" : undefined}>{driver.severeViolationsCount}</strong></td><td>{formatDate(driver.lastImportAtUtc)}</td><td>{formatDays(driver.daysSinceLastActivity)}</td><td>{formatDays(driver.daysSinceLastImport)}</td></tr>)}</tbody>
                            </table>
                        </div>
                    )}
                </>
            ) : null}
        </section>
    );
}

function RiskCard({ label, count, status }: { label: string; count: number; status: string }) {
    return <article className={`driver-risk-card ${status}`}><span>{label}</span><strong>{count}</strong><small>kierowcow</small></article>;
}
