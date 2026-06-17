import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getDriverDownloads,
    getVehicleDownloads,
    type DownloadStatus,
    type DriverDownload,
    type VehicleDownload,
} from "../services/downloadsService";
import "../styles/drivers.css";

type Tab = "drivers" | "vehicles";
type Filter = "All" | DownloadStatus;

const filters: Array<{ value: Filter; label: string }> = [
    { value: "All", label: "Wszystkie" },
    { value: "OK", label: "OK" },
    { value: "Warning", label: "Ostrzeżenie" },
    { value: "Overdue", label: "Po terminie" },
];

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) {
        return "Brak odczytu";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDays(daysUntilDue: number | null) {
    if (daysUntilDue === null) {
        return "Brak odczytu";
    }

    if (daysUntilDue < 0) {
        return `${Math.abs(daysUntilDue)} dni po terminie`;
    }

    if (daysUntilDue === 0) {
        return "Dziś";
    }

    return `${daysUntilDue} dni`;
}

function statusLabel(status: DownloadStatus) {
    if (status === "OK") return "OK";
    if (status === "Warning") return "Ostrzeżenie";
    return "Po terminie";
}

function statusTone(status: DownloadStatus) {
    if (status === "OK") return "success";
    if (status === "Warning") return "warning";
    return "danger";
}

function filterByStatus<T extends { status: DownloadStatus }>(items: T[], filter: Filter) {
    return filter === "All" ? items : items.filter((item) => item.status === filter);
}

export default function DownloadsPage() {
    const [activeTab, setActiveTab] = useState<Tab>("drivers");
    const [activeFilter, setActiveFilter] = useState<Filter>("All");
    const [drivers, setDrivers] = useState<DriverDownload[]>([]);
    const [vehicles, setVehicles] = useState<VehicleDownload[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    const loadDownloads = useCallback(async () => {
        setIsLoading(true);
        setError("");

        try {
            const [driverData, vehicleData] = await Promise.all([
                getDriverDownloads(),
                getVehicleDownloads(),
            ]);

            setDrivers(driverData);
            setVehicles(vehicleData);
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania terminów odczytów.",
            );
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        void loadDownloads();
    }, [loadDownloads]);

    const filteredDrivers = useMemo(
        () => filterByStatus(drivers, activeFilter),
        [activeFilter, drivers],
    );

    const filteredVehicles = useMemo(
        () => filterByStatus(vehicles, activeFilter),
        [activeFilter, vehicles],
    );

    const visibleItemsCount = activeTab === "drivers"
        ? filteredDrivers.length
        : filteredVehicles.length;

    return (
        <div className="drivers-page">
            <div className="drivers-heading">
                <div>
                    <h2>Terminy odczytów</h2>
                    <p>Kontrola cyklicznych pobrań z kart kierowców i tachografów.</p>
                </div>
                <span className="drivers-count">{visibleItemsCount} rekordów</span>
            </div>

            <section className="drivers-panel" style={{ marginTop: 28 }}>
                <div className="section-heading">
                    <h3>Download Compliance</h3>
                    <p>Karty kierowców: 28 dni. Tachografy i pojazdy: 90 dni.</p>
                </div>

                <div className="drivers-toolbar" role="tablist" aria-label="Typ odczytów">
                    <button
                        type="button"
                        onClick={() => setActiveTab("drivers")}
                        aria-selected={activeTab === "drivers"}
                    >
                        Kierowcy
                    </button>
                    <button
                        type="button"
                        onClick={() => setActiveTab("vehicles")}
                        aria-selected={activeTab === "vehicles"}
                    >
                        Pojazdy
                    </button>
                </div>

                <div className="drivers-toolbar" aria-label="Filtry statusu">
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
                    <TableSkeleton rows={6} columns={6} />
                ) : activeTab === "drivers" ? (
                    <DriversTable drivers={filteredDrivers} />
                ) : (
                    <VehiclesTable vehicles={filteredVehicles} />
                )}
            </section>
        </div>
    );
}

function DriversTable({ drivers }: { drivers: DriverDownload[] }) {
    if (drivers.length === 0) {
        return (
            <EmptyState
                title="Brak kierowców dla wybranego filtra"
                description="Zmień filtr albo dodaj import DDD, aby zobaczyć terminy odczytów."
            />
        );
    }

    return (
        <div className="drivers-table-wrapper">
            <table className="drivers-table">
                <thead>
                    <tr>
                        <th>Kierowca</th>
                        <th>Numer karty</th>
                        <th>Ostatni odczyt</th>
                        <th>Następny odczyt</th>
                        <th>Dni do terminu</th>
                        <th>Status</th>
                    </tr>
                </thead>
                <tbody>
                    {drivers.map((driver) => (
                        <tr key={driver.driverId}>
                            <td>
                                <Link className="driver-details-link" to={`/drivers/${driver.driverId}`}>
                                    {[driver.firstName, driver.lastName].filter(Boolean).join(" ") || "Brak danych"}
                                </Link>
                            </td>
                            <td>{driver.cardNumber || "Brak"}</td>
                            <td>{formatDate(driver.lastDownloadUtc)}</td>
                            <td>{formatDate(driver.nextRequiredDownloadUtc)}</td>
                            <td>{formatDays(driver.daysUntilDue)}</td>
                            <td>
                                <StatusBadge label={statusLabel(driver.status)} tone={statusTone(driver.status)} />
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

function VehiclesTable({ vehicles }: { vehicles: VehicleDownload[] }) {
    if (vehicles.length === 0) {
        return (
            <EmptyState
                title="Brak pojazdów dla wybranego filtra"
                description="Zmień filtr albo zaimportuj DDD z użyciem pojazdu."
            />
        );
    }

    return (
        <div className="drivers-table-wrapper">
            <table className="drivers-table">
                <thead>
                    <tr>
                        <th>Rejestracja</th>
                        <th>Ostatni odczyt</th>
                        <th>Następny odczyt</th>
                        <th>Dni do terminu</th>
                        <th>Status</th>
                    </tr>
                </thead>
                <tbody>
                    {vehicles.map((vehicle) => (
                        <tr key={vehicle.vehicleId}>
                            <td>
                                <Link className="driver-details-link" to={`/vehicles/${vehicle.vehicleId}`}>
                                    {vehicle.registrationNumber}
                                </Link>
                            </td>
                            <td>{formatDate(vehicle.lastDownloadUtc)}</td>
                            <td>{formatDate(vehicle.nextRequiredDownloadUtc)}</td>
                            <td>{formatDays(vehicle.daysUntilDue)}</td>
                            <td>
                                <StatusBadge label={statusLabel(vehicle.status)} tone={statusTone(vehicle.status)} />
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
