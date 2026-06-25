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
import TachographTimeline from "../components/tachograph/TachographTimeline";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getVehicleDownloads,
    type DownloadStatus,
    type VehicleDownload,
} from "../services/downloadsService";
import {
    getVehicle,
    getVehicleAnalytics,
    type VehicleActivity,
    type VehicleAnalytics,
    type VehicleDetails,
    type VehicleUseHistory,
} from "../services/vehicleService";
import { formatDriverNameOrFallback } from "../utils/driverName";
import "../styles/driver-details.css";

type EffectiveDownloadStatus = DownloadStatus | "NoData";


type TimelineDay = {
    date: string;
    label: string;
};

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const dayFormatter = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "2-digit",
    month: "long",
    year: "numeric",
    timeZone: "UTC",
});

const chartDayFormatter = new Intl.DateTimeFormat("pl-PL", {
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

    return Number.isNaN(date.getTime()) ? value : chartDayFormatter.format(date);
}

function formatDuration(seconds: number) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    return `${hours} godz. ${minutes} min`;
}

function formatMinutes(minutes: number) {
    return formatDuration(minutes * 60);
}

function getDownloadStatus(download: VehicleDownload | null): EffectiveDownloadStatus {
    if (download?.status === "NoTachographData") {
        return "NoTachographData";
    }

    return download?.lastDownloadUtc ? download.status : "NoData";
}

function getStatusLabel(status: EffectiveDownloadStatus) {
    if (status === "OK") return "OK";
    if (status === "Warning") return "Zbliża się termin";
    if (status === "Overdue") return "Przeterminowany";
    if (status === "NoTachographData") return "Brak danych z tachografu";
    return "Brak danych";
}

function getStatusTone(status: EffectiveDownloadStatus) {
    if (status === "OK") return "success";
    if (status === "Warning") return "warning";
    if (status === "Overdue") return "danger";
    if (status === "NoTachographData") return "info";
    return "neutral";
}

function toUtcDayKey(date: Date) {
    const year = date.getUTCFullYear();
    const month = String(date.getUTCMonth() + 1).padStart(2, "0");
    const day = String(date.getUTCDate()).padStart(2, "0");

    return `${year}-${month}-${day}`;
}

function getUtcDayStart(date: Date) {
    return new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
}

function addDays(date: Date, days: number) {
    return new Date(date.getTime() + days * 24 * 60 * 60 * 1000);
}


function buildTimelineDays(activities: VehicleActivity[]): TimelineDay[] {
    const days = new Map<string, Date>();

    for (const activity of activities) {
        const activityStart = new Date(activity.startUtc);
        const activityEnd = new Date(activity.endUtc);

        if (
            Number.isNaN(activityStart.getTime()) ||
            Number.isNaN(activityEnd.getTime()) ||
            activityEnd <= activityStart
        ) {
            continue;
        }

        let dayCursor = getUtcDayStart(activityStart);

        while (dayCursor < activityEnd) {
            days.set(toUtcDayKey(dayCursor), dayCursor);
            dayCursor = addDays(dayCursor, 1);
        }
    }

    return Array.from(days.entries())
        .map(([date, dayStart]) => ({
            date,
            label: dayFormatter.format(dayStart),
        }))
        .sort((left, right) => right.date.localeCompare(left.date));
}

export default function VehicleDetailsPage() {
    const { vehicleId } = useParams<{ vehicleId: string }>();
    const [details, setDetails] = useState<VehicleDetails | null>(null);
    const [analytics, setAnalytics] = useState<VehicleAnalytics | null>(null);
    const [download, setDownload] = useState<VehicleDownload | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isAnalyticsLoading, setIsAnalyticsLoading] = useState(true);
    const [isDownloadLoading, setIsDownloadLoading] = useState(true);
    const [error, setError] = useState("");
    const [analyticsError, setAnalyticsError] = useState("");
    const [downloadError, setDownloadError] = useState("");

    const chartData = useMemo(
        () => analytics?.dailyUsageLast30Days.map((item) => ({
            ...item,
            label: formatDay(item.date),
            hours: Math.round((item.usageMinutes / 60) * 10) / 10,
        })) ?? [],
        [analytics],
    );

    const timelineDays = useMemo(
        () => buildTimelineDays(details?.activities ?? []),
        [details?.activities],
    );

    useEffect(() => {
        async function loadDetails() {
            if (!vehicleId) {
                setError("Brak identyfikatora pojazdu.");
                setIsLoading(false);
                return;
            }

            setIsLoading(true);
            setError("");

            try {
                setDetails(await getVehicle(vehicleId));
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
    }, [vehicleId]);

    useEffect(() => {
        async function loadAnalytics() {
            if (!vehicleId) {
                setAnalyticsError("Brak identyfikatora pojazdu.");
                setIsAnalyticsLoading(false);
                return;
            }

            setIsAnalyticsLoading(true);
            setAnalyticsError("");

            try {
                setAnalytics(await getVehicleAnalytics(vehicleId));
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
    }, [vehicleId]);

    useEffect(() => {
        async function loadDownload() {
            if (!vehicleId) {
                setIsDownloadLoading(false);
                return;
            }

            setIsDownloadLoading(true);
            setDownloadError("");

            try {
                const vehicles = await getVehicleDownloads();
                setDownload(vehicles.find((vehicle) => vehicle.vehicleId === vehicleId) ?? null);
            } catch (loadError) {
                setDownloadError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania terminu odczytu.",
                );
            } finally {
                setIsDownloadLoading(false);
            }
        }

        void loadDownload();
    }, [vehicleId]);

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

                    <VehicleDownloadSection
                        download={download}
                        error={downloadError}
                        isLoading={isDownloadLoading}
                    />

                    <VehicleTachographTimelineSection activities={details.activities} timelineDays={timelineDays} vehicleUses={details.vehicleUses} />

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
                                    <th>Liczba aktywności</th>
                                </tr>
                            </thead>
                            <tbody>
                                {details.drivers.map((driver) => (
                                    <tr key={driver.driverId}>
                                        <td>
                                            <Link className="table-link" to={`/drivers/${driver.driverId}`}>
                                                {formatDriverNameOrFallback(driver.firstName, driver.lastName)}
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

function VehicleDownloadSection({
    download,
    error,
    isLoading,
}: {
    download: VehicleDownload | null;
    error: string;
    isLoading: boolean;
}) {
    if (isLoading) {
        return (
            <section className="driver-details-section">
                <h3>Odczyty tachografu</h3>
                <TableSkeleton rows={1} columns={4} />
            </section>
        );
    }

    if (error) {
        return (
            <section className="driver-details-section">
                <h3>Odczyty tachografu</h3>
                <p className="driver-details-error" role="alert">
                    {error}
                </p>
            </section>
        );
    }

    const status = getDownloadStatus(download);

    return (
        <section className="driver-details-section">
            <div className="daily-activity-heading">
                <div>
                    <h3>Odczyty tachografu</h3>
                    <p>Termin 90 dni będzie liczony dopiero po realnym odczycie danych tachografu.</p>
                </div>
                <StatusBadge label={getStatusLabel(status)} tone={getStatusTone(status)} />
            </div>

            {status === "NoData" || status === "NoTachographData" ? (
                <EmptyState
                    title="Brak danych z tachografu"
                    description="Pojazd wykryto z karty kierowcy, ale nie ma jeszcze realnego odczytu tachografu."
                />
            ) : (
                <dl className="driver-profile-data" style={{ marginTop: 18 }}>
                    <Info label="Ostatni odczyt" value={formatDate(download?.lastDownloadUtc ?? null)} />
                    <Info label="Następny wymagany odczyt" value={formatDate(download?.nextRequiredDownloadUtc ?? null)} />
                    <Info
                        label="Dni do terminu"
                        value={download?.daysUntilDue == null ? "Brak danych" : String(download.daysUntilDue)}
                    />
                    <Info label="Status" value={getStatusLabel(status)} />
                </dl>
            )}
        </section>
    );
}

function VehicleTachographTimelineSection({
    activities,
    timelineDays,
    vehicleUses,
}: {
    activities: VehicleActivity[];
    timelineDays: TimelineDay[];
    vehicleUses: VehicleUseHistory[];
}) {
    return (
        <section className="driver-details-section tachograph-section">
            <div className="daily-activity-heading">
                <div>
                    <h3>Wykres tachografowy pojazdu</h3>
                    <p>Dzienny widok 00:00-24:00 z aktywności kierowców powiązanych z użyciem pojazdu.</p>
                </div>
            </div>

            {timelineDays.length === 0 ? (
                <EmptyState
                    title="Brak aktywności"
                    description="Wykres tachografowy pojawi się po imporcie aktywności powiązanych z tym pojazdem."
                />
            ) : (
                <div className="tachograph-days">
                    {timelineDays.map((day) => (
                        <TachographTimeline
                            activities={activities}
                            day={day.date}
                            key={day.date}
                            label={day.label}
                            vehicleUses={vehicleUses}
                        />
                    ))}
                </div>
            )}
        </section>
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
                <h3>Użycie pojazdu - ostatnie 30 dni</h3>
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
