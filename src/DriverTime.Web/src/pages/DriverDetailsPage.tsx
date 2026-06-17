import { memo, useEffect, useMemo, useState, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";

import DriverActivityCalendar from "../components/DriverActivityCalendar";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getComplianceViolationsForDriver,
    type ComplianceDriver,
} from "../services/complianceService";
import {
    getDriverDetails,
    type DriverDetails,
} from "../services/driverDetailsService";
import {
    getDriverActivitiesByCard,
    type DriverActivity as TimelineSourceActivity,
} from "../services/driverActivitiesService";
import type { DriverViolation } from "../services/violationsService";
import "../styles/driver-details.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours} godz. ${minutes} min`;
}

const dayFormatter = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "2-digit",
    month: "long",
    year: "numeric",
    timeZone: "UTC",
});

const timeFormatter = new Intl.DateTimeFormat("pl-PL", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
});

const activityLabels: Record<string, string> = {
    DRIVING: "Jazda",
    WORK: "Praca",
    AVAILABILITY: "Dyspozycyjność",
    REST: "Odpoczynek",
};

type TimelineSegment = {
    id: string;
    activityType: string;
    startUtc: Date;
    endUtc: Date;
    leftPercent: number;
    widthPercent: number;
    durationSeconds: number;
};

type TimelineDay = {
    date: string;
    label: string;
    segments: TimelineSegment[];
};

function formatMinutes(minutes: number) {
    return formatDuration(minutes * 60);
}

function formatViolationDuration(violation: DriverViolation) {
    if (violation.actualDurationMinutes <= 0) {
        return "Brak danych";
    }

    const actual = formatMinutes(violation.actualDurationMinutes);
    const exceededBy = Math.max(
        violation.actualDurationMinutes - violation.limitDurationMinutes,
        0,
    );

    return exceededBy > 0
        ? `${actual} (+${formatMinutes(exceededBy)})`
        : actual;
}

function getSeverityClass(severity: string) {
    const normalized = severity.toLowerCase();

    if (normalized === "critical" || normalized === "high" || normalized === "severe") return "high";
    if (normalized === "warning" || normalized === "medium") return "medium";
    if (normalized === "info" || normalized === "low") return "low";
    return "default";
}

function toUtcDayKey(date: Date) {
    const year = date.getUTCFullYear();
    const month = String(date.getUTCMonth() + 1).padStart(2, "0");
    const day = String(date.getUTCDate()).padStart(2, "0");

    return `${year}-${month}-${day}`;
}

function getUtcDayStart(date: Date) {
    return new Date(Date.UTC(
        date.getUTCFullYear(),
        date.getUTCMonth(),
        date.getUTCDate(),
    ));
}

function addDays(date: Date, days: number) {
    return new Date(date.getTime() + days * 24 * 60 * 60 * 1000);
}

function secondsSinceDayStart(date: Date, dayStart: Date) {
    return Math.max(0, (date.getTime() - dayStart.getTime()) / 1000);
}

function getActivityClass(activityType: string) {
    const normalized = activityType.toUpperCase();

    if (normalized === "DRIVING") return "driving";
    if (normalized === "WORK") return "work";
    if (normalized === "AVAILABILITY") return "availability";
    if (normalized === "REST") return "rest";

    return "other";
}

function getActivityLabel(activityType: string) {
    return activityLabels[activityType.toUpperCase()] || activityType || "Inne";
}

function buildDailyTimeline(activities: TimelineSourceActivity[]) {
    const days = new Map<string, TimelineDay>();

    for (const activity of activities) {
        const activityStart = new Date(activity.startUtc);
        const activityEnd = new Date(activity.endUtc);

        if (
            Number.isNaN(activityStart.getTime())
            || Number.isNaN(activityEnd.getTime())
            || activityEnd <= activityStart
        ) {
            continue;
        }

        let segmentStart = activityStart;

        while (segmentStart < activityEnd) {
            const dayStart = getUtcDayStart(segmentStart);
            const dayEnd = addDays(dayStart, 1);
            const segmentEnd = activityEnd < dayEnd ? activityEnd : dayEnd;
            const dayKey = toUtcDayKey(dayStart);
            const durationSeconds = Math.max(
                0,
                (segmentEnd.getTime() - segmentStart.getTime()) / 1000,
            );

            if (!days.has(dayKey)) {
                days.set(dayKey, {
                    date: dayKey,
                    label: dayFormatter.format(dayStart),
                    segments: [],
                });
            }

            days.get(dayKey)!.segments.push({
                id: activity.id,
                activityType: activity.activityType,
                startUtc: segmentStart,
                endUtc: segmentEnd,
                leftPercent: secondsSinceDayStart(segmentStart, dayStart) / 864,
                widthPercent: Math.max(0.2, durationSeconds / 864),
                durationSeconds,
            });

            segmentStart = segmentEnd;
        }
    }

    return Array.from(days.values())
        .map((day) => ({
            ...day,
            segments: day.segments.sort(
                (left, right) => left.startUtc.getTime() - right.startUtc.getTime(),
            ),
        }))
        .sort((left, right) => right.date.localeCompare(left.date));
}

function formatTimelineTooltip(segment: TimelineSegment) {
    return [
        getActivityLabel(segment.activityType),
        `${timeFormatter.format(segment.startUtc)} - ${timeFormatter.format(segment.endUtc)}`,
        formatDuration(segment.durationSeconds),
    ].join("\n");
}

export default function DriverDetailsPage() {
    const { id } = useParams<{ id: string }>();
    const [details, setDetails] = useState<DriverDetails | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");
    const [violations, setViolations] = useState<DriverViolation[]>([]);
    const [areViolationsLoading, setAreViolationsLoading] = useState(true);
    const [violationsError, setViolationsError] = useState("");
    const [timelineActivities, setTimelineActivities] = useState<TimelineSourceActivity[]>([]);
    const [isTimelineLoading, setIsTimelineLoading] = useState(true);
    const [timelineError, setTimelineError] = useState("");

    useEffect(() => {
        async function loadDetails() {
            if (!id) {
                setError("Brak identyfikatora kierowcy.");
                setIsLoading(false);
                return;
            }

            try {
                setDetails(await getDriverDetails(id));
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania kierowcy.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadDetails();
    }, [id]);

    useEffect(() => {
        async function loadViolations() {
            if (!id || !details) {
                setAreViolationsLoading(false);
                return;
            }

            const driver: ComplianceDriver = {
                id: details.id,
                firstName: details.firstName,
                lastName: details.lastName,
                cardNumber: details.cardNumber,
            };

            setAreViolationsLoading(true);
            setViolationsError("");

            try {
                setViolations(await getComplianceViolationsForDriver(driver));
            } catch (loadError) {
                setViolationsError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania naruszeń.",
                );
            } finally {
                setAreViolationsLoading(false);
            }
        }

        void loadViolations();
    }, [details, id]);

    useEffect(() => {
        async function loadTimelineActivities() {
            if (!details?.cardNumber) {
                setTimelineActivities([]);
                setIsTimelineLoading(false);
                return;
            }

            setIsTimelineLoading(true);
            setTimelineError("");

            try {
                setTimelineActivities(await getDriverActivitiesByCard(details.cardNumber));
            } catch (loadError) {
                setTimelineError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania osi aktywności.",
                );
            } finally {
                setIsTimelineLoading(false);
            }
        }

        void loadTimelineActivities();
    }, [details?.cardNumber]);

    const timelineDays = useMemo(
        () => buildDailyTimeline(timelineActivities),
        [timelineActivities],
    );

    return (
        <div className="driver-details-page">
            <Link className="driver-back-link" to="/drivers">
                Powrót do kierowców
            </Link>

            {isLoading && <DriverDetailsSkeleton />}
            {error && <p className="driver-details-error" role="alert">{error}</p>}

            {details && (
                <>
                    <header className="driver-profile-card">
                        <div>
                            <span className="driver-profile-label">Kierowca</span>
                            <h2>{details.firstName} {details.lastName}</h2>
                            <p>{details.cardNumber || "Brak numeru karty"}</p>
                        </div>
                        <dl className="driver-profile-data">
                            <Info label="Ważność karty" value={formatDate(details.cardExpiryDate)} />
                            <Info label="Kraj wydania" value={details.cardIssuingCountry || "Brak danych"} />
                            <Info label="Utworzono" value={formatDate(details.createdAtUtc)} />
                            <Info label="Liczba importów" value={String(details.importsCount)} />
                            <Info label="Ostatni import" value={formatDate(details.lastImportAtUtc)} />
                        </dl>
                    </header>

                    <section className="driver-time-grid" aria-label="Podsumowanie czasów">
                        <TimeCard label="Jazda" seconds={details.drivingSeconds} />
                        <TimeCard label="Praca" seconds={details.workSeconds} />
                        <TimeCard label="Odpoczynek" seconds={details.restSeconds} />
                        <TimeCard label="Dyspozycyjność" seconds={details.availabilitySeconds} />
                    </section>

                    <DriverActivityCalendar driverId={details.id} />

                    <section className="driver-details-section daily-activity-section">
                        <div className="daily-activity-heading">
                            <div>
                                <h3>Dzienna oś aktywności</h3>
                                <p>Pełny widok 24 godzin z rzeczywistych aktywności StartUtc i EndUtc.</p>
                            </div>
                        </div>

                        {isTimelineLoading ? (
                            <div className="activity-calendar-skeleton" aria-busy="true" aria-label="Ładowanie osi aktywności">
                                {Array.from({ length: 4 }, (_, index) => (
                                    <div className="ui-skeleton daily-activity-skeleton" key={index} />
                                ))}
                            </div>
                        ) : timelineError ? (
                            <p className="activity-calendar-error" role="alert">{timelineError}</p>
                        ) : timelineDays.length === 0 ? (
                            <EmptyState
                                title="Brak aktywności"
                                description="Dzienna oś pojawi się po imporcie aktywności kierowcy."
                            />
                        ) : (
                            <div className="daily-activity-days">
                                {timelineDays.map((day) => (
                                    <article className="daily-activity-day" key={day.date}>
                                        <h4>{day.label}</h4>
                                        <div className="daily-activity-timeline" aria-label={`Dzienna oś aktywności ${day.date}`}>
                                            <span className="daily-activity-baseline" />
                                            {day.segments.map((segment, index) => (
                                                <span
                                                    className={`daily-activity-segment ${getActivityClass(segment.activityType)}`}
                                                    key={`${segment.id}-${segment.startUtc.toISOString()}-${index}`}
                                                    style={{
                                                        left: `${segment.leftPercent}%`,
                                                        width: `${segment.widthPercent}%`,
                                                    }}
                                                    title={formatTimelineTooltip(segment)}
                                                    aria-label={formatTimelineTooltip(segment)}
                                                />
                                            ))}
                                        </div>
                                        <div className="timeline-hours">
                                            <span>00:00</span>
                                            <span>06:00</span>
                                            <span>12:00</span>
                                            <span>18:00</span>
                                            <span>24:00</span>
                                        </div>
                                    </article>
                                ))}
                            </div>
                        )}
                    </section>

                    <DetailsSection title="Historia importów" empty={details.recentImports.length === 0}>
                        <table><thead><tr><th>Plik</th><th>Data</th><th>Aktywności</th><th></th></tr></thead>
                            <tbody>{details.recentImports.map((item) => <tr key={item.id}><td>{item.fileName}</td><td>{formatDate(item.uploadedAtUtc)}</td><td>{item.activitiesCount}</td><td><Link className="table-link" to={`/imports/${item.id}`}>Szczegóły</Link></td></tr>)}</tbody>
                        </table>
                    </DetailsSection>

                    <DetailsSection title="Ostatnie aktywności" empty={details.recentActivities.length === 0}>
                        <table><thead><tr><th>Typ</th><th>Początek</th><th>Koniec</th><th>Czas</th></tr></thead>
                            <tbody>{details.recentActivities.map((item) => <tr key={item.id}><td>{item.activityType}</td><td>{formatDate(item.startUtc)}</td><td>{formatDate(item.endUtc)}</td><td>{formatDuration(item.durationSeconds)}</td></tr>)}</tbody>
                        </table>
                    </DetailsSection>

                    <section className="driver-details-section">
                        <h3>Naruszenia</h3>

                        {areViolationsLoading ? (
                            <TableSkeleton rows={4} columns={5} />
                        ) : violationsError ? (
                            <p className="driver-violations-error" role="alert">
                                {violationsError}
                            </p>
                        ) : violations.length === 0 ? (
                            <EmptyState
                                title="Brak naruszeń"
                                description="Engine compliance nie wykrył naruszeń dla aktywności tego kierowcy."
                            />
                        ) : (
                            <div className="driver-details-table violations-table">
                                <table>
                                    <thead><tr><th>Data i czas</th><th>Typ</th><th>Opis</th><th>Poziom</th><th>Czas / przekroczenie</th></tr></thead>
                                    <tbody>{violations.map((item, index) => <tr key={`${item.code}-${item.occurredAtUtc}-${index}`}><td>{formatDate(item.occurredAtUtc)}</td><td>{item.violationType}</td><td>{item.description}</td><td><span className={`severity-badge ${getSeverityClass(item.severity)}`}>{item.severity}</span></td><td>{formatViolationDuration(item)}</td></tr>)}</tbody>
                                </table>
                            </div>
                        )}
                    </section>

                    <DetailsSection title="Użyte pojazdy" empty={details.vehicles.length === 0}>
                        <table><thead><tr><th>Numer rejestracyjny</th><th>Pierwsze użycie</th><th>Ostatnie użycie</th><th>Liczba użyć</th></tr></thead>
                            <tbody>{details.vehicles.map((item) => <tr key={item.registrationNumber}><td>{item.registrationNumber}</td><td>{formatDate(item.firstUsedAtUtc)}</td><td>{formatDate(item.lastUsedAtUtc)}</td><td>{item.usageCount}</td></tr>)}</tbody>
                        </table>
                    </DetailsSection>
                </>
            )}
        </div>
    );
}

const Info = memo(function Info({ label, value }: { label: string; value: string }) {
    return <div><dt>{label}</dt><dd>{value}</dd></div>;
});

const TimeCard = memo(function TimeCard({ label, seconds }: { label: string; seconds: number }) {
    return <article><span>{label}</span><strong>{formatDuration(seconds)}</strong></article>;
});

function DetailsSection({ title, empty, children }: { title: string; empty: boolean; children: ReactNode }) {
    return <section className="driver-details-section"><h3>{title}</h3>{empty ? <EmptyState title={`Brak: ${title.toLocaleLowerCase("pl-PL")}`} description="Dane pojawią się po kolejnym imporcie pliku DDD." /> : <div className="driver-details-table">{children}</div>}</section>;
}

function DriverDetailsSkeleton() {
    return (
        <div className="driver-details-skeleton" aria-busy="true" aria-label="Ładowanie kierowcy">
            <div className="ui-skeleton driver-profile-skeleton" />
            <div className="driver-time-grid">
                {Array.from({ length: 4 }, (_, index) => <div className="ui-skeleton driver-time-skeleton" key={index} />)}
            </div>
            <div className="ui-skeleton driver-section-skeleton" />
            <div className="ui-skeleton driver-section-skeleton" />
        </div>
    );
}
