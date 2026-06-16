import {
    memo,
    useDeferredValue,
    useEffect,
    useMemo,
    useState,
} from "react";
import { Link } from "react-router-dom";

import {
    getDriverRiskOverview,
    type DriverRisk,
    type DriverRiskOverview as DriverRiskOverviewData,
} from "../services/dashboardService";
import Pagination from "./Pagination";
import { EmptyState, TableSkeleton } from "./UiStates";

const pageSize = 8;
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
    return { Low: "Niskie", Medium: "Średnie", High: "Wysokie", Critical: "Krytyczne" }[status];
}

function DriverRiskOverview() {
    const [overview, setOverview] = useState<DriverRiskOverviewData | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");
    const [search, setSearch] = useState("");
    const [riskFilter, setRiskFilter] = useState("All");
    const [currentPage, setCurrentPage] = useState(1);
    const deferredSearch = useDeferredValue(search.trim().toLocaleLowerCase("pl-PL"));

    useEffect(() => {
        async function loadRiskOverview() {
            try {
                setOverview(await getDriverRiskOverview());
            } catch (loadError) {
                setError(loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania ryzyka kierowców.");
            } finally {
                setIsLoading(false);
            }
        }
        void loadRiskOverview();
    }, []);

    const filteredDrivers = useMemo(() => {
        const drivers = overview?.drivers ?? [];
        return drivers.filter((driver) => {
            const matchesRisk = riskFilter === "All" || driver.riskStatus === riskFilter;
            const haystack = `${driver.firstName} ${driver.lastName} ${driver.cardNumber}`
                .toLocaleLowerCase("pl-PL");
            return matchesRisk && (!deferredSearch || haystack.includes(deferredSearch));
        });
    }, [deferredSearch, overview, riskFilter]);

    const visibleDrivers = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return filteredDrivers.slice(start, start + pageSize);
    }, [currentPage, filteredDrivers]);

    const severeTotal = useMemo(() =>
        overview?.drivers.reduce((sum, driver) => sum + driver.severeViolationsCount, 0) ?? 0,
    [overview]);

    useEffect(() => setCurrentPage(1), [deferredSearch, riskFilter]);

    return (
        <section className="driver-risk-section">
            <div className="panel-heading">
                <div>
                    <h3>Ryzyko kierowców</h3>
                    <p>Alerty na podstawie naruszeń i aktualności danych DDD.</p>
                </div>
            </div>

            {isLoading ? (
                <><div className="driver-risk-loading">{Array.from({ length: 4 }, (_, index) => <div className="skeleton risk-skeleton-card" key={index} />)}</div><TableSkeleton rows={5} columns={8} /></>
            ) : error ? (
                <p className="driver-risk-error" role="alert">{error}</p>
            ) : overview ? (
                <>
                    <div className="driver-risk-cards">
                        <RiskCard label="Niskie" count={overview.lowRiskCount} status="low" />
                        <RiskCard label="Średnie" count={overview.mediumRiskCount} status="medium" />
                        <RiskCard label="Wysokie" count={overview.highRiskCount} status="high" />
                        <RiskCard label="Krytyczne" count={overview.criticalRiskCount} status="critical" />
                    </div>

                    <div className="risk-quick-stats">
                        <span><strong>{overview.drivers.length}</strong> monitorowanych</span>
                        <span><strong>{overview.highRiskCount + overview.criticalRiskCount}</strong> wymaga reakcji</span>
                        <span><strong>{severeTotal}</strong> ciężkich naruszeń</span>
                    </div>

                    <div className="risk-toolbar">
                        <input
                            type="search"
                            aria-label="Szukaj kierowcy ryzyka"
                            placeholder="Nazwisko lub numer karty"
                            value={search}
                            onChange={(event) => setSearch(event.target.value)}
                        />
                        <select
                            aria-label="Filtr statusu ryzyka"
                            value={riskFilter}
                            onChange={(event) => setRiskFilter(event.target.value)}
                        >
                            <option value="All">Wszystkie statusy</option>
                            <option value="Low">Niskie</option>
                            <option value="Medium">Średnie</option>
                            <option value="High">Wysokie</option>
                            <option value="Critical">Krytyczne</option>
                        </select>
                    </div>

                    {overview.drivers.length === 0 ? (
                        <EmptyState title="Brak kierowców" description="Zaimportuj dane DDD, aby uruchomić analizę ryzyka floty." />
                    ) : filteredDrivers.length === 0 ? (
                        <EmptyState title="Brak wyników" description="Zmień wyszukiwanie lub wybrany status ryzyka." />
                    ) : (
                        <>
                            <div className="dashboard-table-wrapper driver-risk-table-wrapper">
                                <table className="dashboard-table driver-risk-table">
                                    <thead><tr><th>Kierowca</th><th>Karta</th><th>Status</th><th>Naruszenia</th><th>Ciężkie</th><th>Ostatni import</th><th>Bez aktywności</th><th>Bez importu</th></tr></thead>
                                    <tbody>{visibleDrivers.map((driver) => <RiskRow driver={driver} key={driver.driverId} />)}</tbody>
                                </table>
                            </div>
                            <Pagination currentPage={currentPage} pageSize={pageSize} totalItems={filteredDrivers.length} onPageChange={setCurrentPage} />
                        </>
                    )}
                </>
            ) : null}
        </section>
    );
}

const RiskRow = memo(function RiskRow({ driver }: { driver: DriverRisk }) {
    return (
        <tr className={driver.riskStatus === "Critical" ? "critical-risk-row" : undefined}>
            <td><Link to={`/drivers/${driver.driverId}`}>{`${driver.firstName} ${driver.lastName}`.trim() || "Brak danych"}</Link></td>
            <td>{driver.cardNumber || "Brak danych"}</td>
            <td><span className={`risk-badge ${driver.riskStatus.toLowerCase()}`}>{getRiskLabel(driver.riskStatus)}</span></td>
            <td>{driver.violationsCount}</td>
            <td><strong className={driver.severeViolationsCount > 0 ? "severe-count" : undefined}>{driver.severeViolationsCount}</strong></td>
            <td>{formatDate(driver.lastImportAtUtc)}</td>
            <td>{formatDays(driver.daysSinceLastActivity)}</td>
            <td>{formatDays(driver.daysSinceLastImport)}</td>
        </tr>
    );
});

const RiskCard = memo(function RiskCard({ label, count, status }: { label: string; count: number; status: string }) {
    return <article className={`driver-risk-card ${status}`}><span>{label}</span><strong>{count}</strong><small>kierowców</small></article>;
});

export default memo(DriverRiskOverview);
