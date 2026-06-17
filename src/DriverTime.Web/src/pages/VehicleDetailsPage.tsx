import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";
import {
    Bar,
    BarChart,
    CartesianGrid,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getVehicle,
    getVehicleAnalytics,
    type VehicleAnalytics,
    type VehicleDetails,
} from "../services/vehicleService";
import "../styles/driver-details.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const dayFormatter = new Intl.DateTimeFormat("pl-PL", {
    day: "2-digit",
    month: "2-digit",
});

function formatDate(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(value);

    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDay(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime()) ? value : dayFormatter.format(date);
}

function formatDriverName(firstName: string, lastName: string) {
    const fullName = `${firstName} ${lastName}`.trim();

    return fullName || "Brak danych";
}

function formatMinutes(minutes: number) {
    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;

    if (hours <= 0) {
        return `${remainingMinutes} min`;
    }

    return `${hours} godz. ${remainingMinutes} min`;
}

export default function VehicleDetailsPage() {
    const { id } = useParams<{ id: string }>();
    const [details, setDetails] = useState<VehicleDetails | null>(null);
    const [analytics, setAnalytics] = useState<VehicleAnalytics | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isAnalyticsLoading, setIsAnalyticsLoading] = useState(true);
    const [error, setError] = useState("");
    const [analyticsError, setAnalyticsError] = useState("");

    const chartData = useMemo(
        () => analytics?.dailyUsageLast30Days.map((item) => ({
            ...item,
            label: formatDay(item.date),
            hours: Math.round((item.usageMinutes / 60) * 10) / 10,
        })) ?? [],
        [analytics],
    );

    useEffect(() => {
        async function loadDetails() {
            if (!id) {
                setError("Brak identyfikatora pojazdu.");
                setIsLoading(false);
                return;
            }

            setIsLoading(true);
            setError("");

            try {
                setDetails(await getVehicle(id));
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania pojazdu.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadDetails();
    }, [id]);

    useEffect(() => {
        async function loadAnalytics() {
            if (!id) {
                setAnalyticsError("Brak identyfikatora pojazdu.");
                setIsAnalyticsLoading(false);
                return;
            }

            setIsAnalyticsLoading(true);
            setAnalyticsError("");

            try {
                setAnalytics(await getVehicleAnalytics(id));
            } catch (loadError) {
                setAnalyticsError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania analityki pojazdu.",
                );
            } finally {
                setIsAnalyticsLoading(false);
            }
        }

        void loadAnalytics();
    }, [id]);

    return (
        <div className="driver-details-page">
            <Link className="driver-back-link" to="/vehicles">
                Powrót do pojazdów
            </Link>

            {isLoading ? <VehicleDetailsSkeleton /> : null}

            {error ? (
                <p className="driver-details-error" role="alert">
                    {error}
                </p>
            ) : null}

            {!isLoading && !error && !details ? (
                <EmptyState
                    title="Brak danych pojazdu"
                    description="Nie znaleziono szczegółów dla wybranego pojazdu."
                />
            ) : null}

            {details ? (
                <>
                    <header className="driver-profile-card">
                        <div>
                            <span className="driver-profile-label">Pojazd</span>
                            <h2>{details.registrationNumber}</h2>
                            <p>{details.vin || "VIN: brak"}</p>
                        </div>
                        <dl className="driver-profile-data">
                            <Info label="Status" value={details.active ? "Aktywny" : "Nieaktywny"} />
                            <Info label="VIN" value={details.vin || "Brak"} />
                            <Info label="Ostatnia aktywność" value={formatDate(details.lastActivityAtUtc)} />
                            <Info label="Liczba importów DDD" value={String(details.dddImportsCount)} />
                            <Info label="Liczba użyć" value={String(details.vehicleUses.length)} />
                            <Info label="Liczba kierowców" value={String(details.drivers.length)} />
                        </dl>
                    </header>

                    <section className="driver-time-grid" aria-label="Podsumowanie pojazdu">
                        <article>
                            <span>Status</span>
                            <strong>
                                <StatusBadge
                                    label={details.active ? "Aktywny" : "Nieaktywny"}
                                    tone={details.active ? "success" : "neutral"}
                                />
                            </strong>
                        </article>
                        <SummaryCard label="Importy DDD" value={String(details.dddImportsCount)} />
                        <SummaryCard label="Użycia pojazdu" value={String(details.vehicleUses.length)} />
                        <SummaryCard label="Kierowcy" value={String(details.drivers.length)} />
                    </section>

                    <VehicleAnalyticsSection
                        analytics={analytics}
                        chartData={chartData}
                        error={analyticsError}
                        isLoading={isAnalyticsLoading}
                    />

                    <DetailsSection title="Historia użycia pojazdu" empty={details.vehicleUses.length === 0}>
                        <table>
                            <thead>
                                <tr>
                                    <th>Rejestracja</th>
                                    <th>Początek</th>
                                    <th>Koniec</th>
                                    <th>Kierowca</th>
                                    <th>Import DDD</th>
                                    <th>Data importu</th>
                                </tr>
                            </thead>
                            <tbody>
                                {details.vehicleUses.map((item) => (
                                    <tr key={item.id}>
                                        <td>{item.registrationNumber}</td>
                                        <td>{formatDate(item.startUtc)}</td>
                                        <td>{formatDate(item.endUtc)}</td>
                                        <td>
                                            {item.driverId ? (
                                                <Link className="table-link" to={`/drivers/${item.driverId}`}>
                                                    {item.driverName.trim() || "Kierowca"}
                                                </Link>
                                            ) : (
                                                "Brak danych"
                                            )}
                                        </td>
                                        <td>
                                            <Link className="table-link" to={`/imports/${item.dddFileId}`}>
                                                {item.fileName || "Szczegóły importu"}
                                            </Link>
                                        </td>
                                        <td>{formatDate(item.uploadedAtUtc)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </DetailsSection>

                    <DetailsSection title="Kierowcy używający pojazdu" empty={details.drivers.length === 0}>
                        <table>
                            <thead>
                                <tr>
                                    <th>Kierowca</th>
                                    <th>Numer karty</th>
                                    <th>Pierwsze użycie</th>
                                    <th>Ostatnie użycie</th>
                                    <th>Liczba użyć</th>
                                </tr>
                            </thead>
                            <tbody>
                                {details.drivers.map((driver) => (
                                    <tr key={driver.driverId}>
                                        <td>
                                            <Link className="table-link" to={`/drivers/${driver.driverId}`}>
                                                {formatDriverName(driver.firstName, driver.lastName)}
                                            </Link>
                                        </td>
                                        <td>{driver.cardNumber || "Brak"}</td>
                                        <td>{formatDate(driver.firstUsedAtUtc)}</td>
                                        <td>{formatDate(driver.lastUsedAtUtc)}</td>
                                        <td>{driver.usageCount}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </DetailsSection>
                </>
            ) : null}
        </div>
    );
}

function VehicleAnalyticsSection({
    analytics,
    chartData,
    error,
    isLoading,
}: {
    analytics: VehicleAnalytics | null;
    chartData: Array<{ date: string; label: string; usesCount: number; usageMinutes: number; hours: number }>;
    error: string;
    isLoading: boolean;
}) {
    if (isLoading) {
        return (
            <section className="driver-details-section">
                <h3>Analityka wykorzystania</h3>
                <TableSkeleton rows={4} columns={4} />
            </section>
        );
    }

    if (error) {
        return (
            <section className="driver-details-section">
                <h3>Analityka wykorzystania</h3>
                <p className="driver-details-error" role="alert">
                    {error}
                </p>
            </section>
        );
    }

    if (!analytics || analytics.totalUses === 0) {
        return (
            <section className="driver-details-section">
                <h3>Analityka wykorzystania</h3>
                <EmptyState
                    title="Brak danych analitycznych"
                    description="Analityka pojawi się po zapisaniu użyć pojazdu z importów DDD."
                />
            </section>
        );
    }

    return (
        <>
            <section className="driver-time-grid" aria-label="KPI wykorzystania pojazdu">
                <SummaryCard label="Liczba użyć" value={String(analytics.totalUses)} />
                <SummaryCard label="Kierowcy" value={String(analytics.totalDrivers)} />
                <SummaryCard label="Importy DDD" value={String(analytics.totalDddImports)} />
                <SummaryCard label="Pierwsze użycie" value={formatDate(analytics.firstUseUtc)} />
                <SummaryCard label="Ostatnie użycie" value={formatDate(analytics.lastUseUtc)} />
                <SummaryCard label="Aktywność 7 dni" value={String(analytics.usesLast7Days)} />
                <SummaryCard label="Aktywność 30 dni" value={String(analytics.usesLast30Days)} />
            </section>

            <section className="driver-time-grid" aria-label="Wykorzystanie pojazdu">
                <SummaryCard label="Łączny czas użycia" value={formatMinutes(analytics.totalUsageMinutes)} />
                <SummaryCard label="Łącznie godzin" value={`${analytics.totalUsageHours} godz.`} />
                <SummaryCard label="Aktywne dni" value={String(analytics.activeDays)} />
                <SummaryCard
                    label="Średnio na aktywny dzień"
                    value={formatMinutes(Math.round(analytics.averageUsageMinutesPerActiveDay))}
                />
            </section>

            <section className="driver-details-section">
                <h3>Użycie pojazdu — ostatnie 30 dni</h3>
                {chartData.every((item) => item.usesCount === 0 && item.usageMinutes === 0) ? (
                    <EmptyState
                        title="Brak użyć w ostatnich 30 dniach"
                        description="Wykres pojawi się, gdy pojazd będzie miał użycia w tym okresie."
                    />
                ) : (
                    <div style={{ width: "100%", height: 320 }}>
                        <ResponsiveContainer width="100%" height="100%">
                            <BarChart data={chartData} margin={{ top: 8, right: 18, left: 0, bottom: 0 }}>
                                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                                <XAxis dataKey="label" tickLine={false} axisLine={false} />
                                <YAxis yAxisId="left" tickLine={false} axisLine={false} allowDecimals={false} />
                                <YAxis yAxisId="right" orientation="right" tickLine={false} axisLine={false} />
                                <Tooltip
                                    formatter={(value, name) => [
                                        name === "hours" ? `${value} godz.` : value,
                                        name === "hours" ? "Czas użycia" : "Liczba użyć",
                                    ]}
                                    labelFormatter={(_, payload) => {
                                        const item = payload?.[0]?.payload as { date?: string } | undefined;
                                        return item?.date ? formatDay(item.date) : "";
                                    }}
                                />
                                <Bar yAxisId="left" dataKey="usesCount" name="usesCount" fill="#2563eb" radius={[5, 5, 0, 0]} />
                                <Bar yAxisId="right" dataKey="hours" name="hours" fill="#16a34a" radius={[5, 5, 0, 0]} />
                            </BarChart>
                        </ResponsiveContainer>
                    </div>
                )}
            </section>

            <DetailsSection title="Najczęściej używający kierowcy" empty={analytics.driverUsage.length === 0}>
                <table>
                    <thead>
                        <tr>
                            <th>Kierowca</th>
                            <th>Numer karty</th>
                            <th>Liczba użyć</th>
                            <th>Czas użycia</th>
                            <th>Pierwsze użycie</th>
                            <th>Ostatnie użycie</th>
                        </tr>
                    </thead>
                    <tbody>
                        {analytics.driverUsage.map((driver) => (
                            <tr key={driver.driverId}>
                                <td>
                                    <Link className="table-link" to={`/drivers/${driver.driverId}`}>
                                        {driver.driverName || "Brak danych"}
                                    </Link>
                                </td>
                                <td>{driver.cardNumber || "Brak"}</td>
                                <td>{driver.usesCount}</td>
                                <td>{formatMinutes(driver.usageMinutes)}</td>
                                <td>{formatDate(driver.firstUseUtc)}</td>
                                <td>{formatDate(driver.lastUseUtc)}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </DetailsSection>
        </>
    );
}

function Info({ label, value }: { label: string; value: string }) {
    return (
        <div>
            <dt>{label}</dt>
            <dd>{value}</dd>
        </div>
    );
}

function SummaryCard({ label, value }: { label: string; value: string }) {
    return (
        <article>
            <span>{label}</span>
            <strong>{value}</strong>
        </article>
    );
}

function DetailsSection({
    title,
    empty,
    children,
}: {
    title: string;
    empty: boolean;
    children: ReactNode;
}) {
    return (
        <section className="driver-details-section">
            <h3>{title}</h3>
            {empty ? (
                <EmptyState
                    title={`Brak: ${title.toLocaleLowerCase("pl-PL")}`}
                    description="Dane pojawią się po imporcie plików DDD powiązanych z tym pojazdem."
                />
            ) : (
                <div className="driver-details-table">{children}</div>
            )}
        </section>
    );
}

function VehicleDetailsSkeleton() {
    return (
        <div className="driver-details-skeleton" aria-busy="true" aria-label="Ładowanie pojazdu">
            <div className="ui-skeleton driver-profile-skeleton" />
            <div className="driver-time-grid">
                {Array.from({ length: 4 }, (_, index) => (
                    <div className="ui-skeleton driver-time-skeleton" key={index} />
                ))}
            </div>
            <section className="driver-details-section">
                <TableSkeleton rows={4} columns={6} />
            </section>
        </div>
    );
}
