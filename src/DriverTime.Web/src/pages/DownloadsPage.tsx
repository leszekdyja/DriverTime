import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getDownloadDashboard,
    getDriverDownloads,
    getVehicleDownloads,
    type DownloadDashboard,
    type DownloadStatus,
    type DriverDownload,
    type VehicleDownload,
} from "../services/downloadsService";
import "../styles/dashboard.css";
import "../styles/drivers.css";
import "../styles/downloads.css";

type Filter = "All" | DownloadStatus | "NoData";

const filters: Array<{ value: Filter; label: string }> = [
    { value: "All", label: "Wszystkie" },
    { value: "OK", label: "OK" },
    { value: "Warning", label: "Zbliża się termin" },
    { value: "Overdue", label: "Przeterminowane" },
    { value: "NoTachographData", label: "Brak danych z tachografu" },
    { value: "NoData", label: "Brak danych" },
];

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) {
        return "Brak danych";
    }

    const date = new Date(value);

    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDays(daysUntilDue: number | null) {
    if (daysUntilDue === null) {
        return "Brak danych";
    }

    if (daysUntilDue < 0) {
        return `${Math.abs(daysUntilDue)} dni po terminie`;
    }

    if (daysUntilDue === 0) {
        return "Dziś";
    }

    return `${daysUntilDue} dni`;
}

function getEffectiveStatus(item: { lastDownloadUtc: string | null; status: DownloadStatus }): Filter {
    if (item.status === "NoTachographData") {
        return "NoTachographData";
    }

    return item.lastDownloadUtc ? item.status : "NoData";
}

function statusLabel(status: Filter) {
    if (status === "OK") return "OK";
    if (status === "Warning") return "Zbliża się termin";
    if (status === "Overdue") return "Przeterminowany";
    if (status === "NoTachographData") return "Brak danych z tachografu";
    if (status === "NoData") return "Brak danych";
    return "Wszystkie";
}

function statusTone(status: Filter) {
    if (status === "OK") return "success";
    if (status === "Warning") return "warning";
    if (status === "Overdue") return "danger";
    if (status === "NoTachographData") return "info";
    if (status === "NoData") return "info";
    return "neutral";
}

function filterByStatus<T extends { lastDownloadUtc: string | null; status: DownloadStatus }>(
    items: T[],
    filter: Filter,
) {
    return filter === "All"
        ? items
        : items.filter((item) => getEffectiveStatus(item) === filter);
}

function countByStatus<T extends { lastDownloadUtc: string | null; status: DownloadStatus }>(
    items: T[],
    status: Filter,
) {
    return items.filter((item) => getEffectiveStatus(item) === status).length;
}

function getDriverName(driver: DriverDownload) {
    return [driver.firstName, driver.lastName].filter(Boolean).join(" ") || "Brak danych";
}

export default function DownloadsPage() {
    const [activeFilter, setActiveFilter] = useState<Filter>("All");
    const [dashboard, setDashboard] = useState<DownloadDashboard | null>(null);
    const [drivers, setDrivers] = useState<DriverDownload[]>([]);
    const [vehicles, setVehicles] = useState<VehicleDownload[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    const loadDownloads = useCallback(async () => {
        setIsLoading(true);
        setError("");

        try {
            const [summaryData, driverData, vehicleData] = await Promise.all([
                getDownloadDashboard(),
                getDriverDownloads(),
                getVehicleDownloads(),
            ]);

            setDashboard(summaryData);
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

    const okDownloads = countByStatus(drivers, "OK") + countByStatus(vehicles, "OK");
    const dueSoonDownloads = (dashboard?.warningDrivers ?? countByStatus(drivers, "Warning")) +
        (dashboard?.warningVehicles ?? countByStatus(vehicles, "Warning"));
    const overdueDownloads = (dashboard?.overdueDrivers ?? countByStatus(drivers, "Overdue")) +
        (dashboard?.overdueVehicles ?? countByStatus(vehicles, "Overdue"));
    const noDataDownloads = countByStatus(drivers, "NoData") +
        countByStatus(vehicles, "NoData") +
        countByStatus(vehicles, "NoTachographData");

    return (
        <div className="downloads-page">
            <header className="downloads-heading">
                <div>
                    <span>Odczyty</span>
                    <h2>Terminy pobrań kart i tachografów</h2>
                    <p>
                        Karta kierowcy powinna być pobierana maksymalnie co 28 dni.
                        Termin tachografu pojawi się po realnym odczycie danych z pojazdu.
                    </p>
                </div>
                <strong>{filteredDrivers.length + filteredVehicles.length} rekordów po filtrze</strong>
            </header>

            <section className="downloads-summary" aria-label="Podsumowanie terminów odczytów">
                <SummaryCard label="OK" value={okDownloads} tone="green" description="Więcej niż 7 dni do terminu" />
                <SummaryCard label="Zbliża się termin" value={dueSoonDownloads} tone="amber" description="7 dni lub mniej" />
                <SummaryCard label="Przeterminowane" value={overdueDownloads} tone="red" description="Termin odczytu minął" />
                <SummaryCard label="Brak danych" value={noDataDownloads} tone="blue" description="Brak realnego odczytu lub daty źródłowej" />
            </section>

            <section className="downloads-rules" aria-label="Zasady terminów">
                <article>
                    <span>28 dni</span>
                    <strong>Karta kierowcy</strong>
                    <p>Następny wymagany odczyt jest liczony jako ostatni odczyt + 28 dni.</p>
                </article>
                <article>
                    <span>90 dni</span>
                    <strong>Tachograf / pojazd</strong>
                    <p>Termin 90 dni będzie liczony dopiero z realnego odczytu tachografu, a nie z użycia pojazdu na karcie kierowcy.</p>
                </article>
            </section>

            <section className="downloads-filters" aria-label="Filtry statusu">
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
            </section>

            {error ? (
                <p className="downloads-error" role="alert">
                    {error}
                </p>
            ) : null}

            {isLoading ? (
                <section className="downloads-section" aria-busy="true">
                    <TableSkeleton rows={8} columns={6} />
                </section>
            ) : (
                <>
                    <section className="downloads-section">
                        <SectionHeading
                            title="Kierowcy"
                            description="Terminy pobrań kart kierowców na podstawie ostatnich dostępnych odczytów."
                            count={filteredDrivers.length}
                        />
                        <DriversTable drivers={filteredDrivers} />
                    </section>

                    <section className="downloads-section">
                        <SectionHeading
                            title="Pojazdy i tachografy"
                            description="Pojazdy wykryte z kart kierowców. Termin tachografu wymaga realnego odczytu danych z pojazdu."
                            count={filteredVehicles.length}
                        />
                        <VehiclesTable vehicles={filteredVehicles} />
                    </section>
                </>
            )}
        </div>
    );
}

function SummaryCard({
    label,
    value,
    tone,
    description,
}: {
    label: string;
    value: number;
    tone: "green" | "amber" | "red" | "blue";
    description: string;
}) {
    return (
        <article className={`downloads-summary-card ${tone}`}>
            <span>{label}</span>
            <strong>{value}</strong>
            <small>{description}</small>
        </article>
    );
}

function SectionHeading({
    title,
    description,
    count,
}: {
    title: string;
    description: string;
    count: number;
}) {
    return (
        <div className="downloads-section-heading">
            <div>
                <h3>{title}</h3>
                <p>{description}</p>
            </div>
            <span>{count} rekordów</span>
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
        <div className="downloads-table-wrapper">
            <table className="downloads-table">
                <thead>
                    <tr>
                        <th>Kierowca</th>
                        <th>Numer karty</th>
                        <th>Ostatni odczyt</th>
                        <th>Następny wymagany odczyt</th>
                        <th>Dni do terminu</th>
                        <th>Status</th>
                    </tr>
                </thead>
                <tbody>
                    {drivers.map((driver) => {
                        const status = getEffectiveStatus(driver);

                        return (
                            <tr key={driver.driverId}>
                                <td data-label="Kierowca">
                                    <Link className="driver-details-link" to={`/drivers/${driver.driverId}`}>
                                        {getDriverName(driver)}
                                    </Link>
                                </td>
                                <td data-label="Numer karty">{driver.cardNumber || "Brak"}</td>
                                <td data-label="Ostatni odczyt">{formatDate(driver.lastDownloadUtc)}</td>
                                <td data-label="Następny odczyt">{formatDate(driver.nextRequiredDownloadUtc)}</td>
                                <td data-label="Dni do terminu">{formatDays(driver.daysUntilDue)}</td>
                                <td data-label="Status">
                                    <StatusBadge label={statusLabel(status)} tone={statusTone(status)} />
                                </td>
                            </tr>
                        );
                    })}
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
        <div className="downloads-table-wrapper">
            <table className="downloads-table">
                <thead>
                    <tr>
                        <th>Rejestracja</th>
                        <th>Ostatni odczyt</th>
                        <th>Następny wymagany odczyt</th>
                        <th>Dni do terminu</th>
                        <th>Status</th>
                    </tr>
                </thead>
                <tbody>
                    {vehicles.map((vehicle) => {
                        const status = getEffectiveStatus(vehicle);

                        return (
                            <tr key={vehicle.vehicleId}>
                                <td data-label="Rejestracja">
                                    <Link className="driver-details-link" to={`/vehicles/${vehicle.vehicleId}`}>
                                        {vehicle.registrationNumber}
                                    </Link>
                                </td>
                                <td data-label="Ostatni odczyt">{formatDate(vehicle.lastDownloadUtc)}</td>
                                <td data-label="Następny odczyt">{formatDate(vehicle.nextRequiredDownloadUtc)}</td>
                                <td data-label="Dni do terminu">{formatDays(vehicle.daysUntilDue)}</td>
                                <td data-label="Status">
                                    <StatusBadge label={statusLabel(status)} tone={statusTone(status)} />
                                </td>
                            </tr>
                        );
                    })}
                </tbody>
            </table>
        </div>
    );
}
