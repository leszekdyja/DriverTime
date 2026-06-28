import { useMemo } from "react";
import { getComplianceRuleLabel } from "../../utils/complianceLabels";
import "./TachographTimeline.css";

export type TachographActivity = {
    id: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds?: number;
    vehicleRegistration?: string | null;
    vehicleRegistrationNumber?: string | null;
    registrationNumber?: string | null;
};

export type TachographCountryEntry = {
    id?: string;
    timestamp?: string;
    timestampUtc?: string;
    entryTimeUtc?: string;
    timeUtc?: string;
    occurredAtUtc?: string;
    countryCode?: string;
    countryName?: string;
    country_code?: string;
    country_name?: string;
    entryType?: string | null;
    type?: string | null;
};

export type TachographCardReading = {
    id?: string;
    timestampUtc?: string;
    timeUtc?: string;
    occurredAtUtc?: string;
    readAtUtc?: string;
    label?: string | null;
};

export type TachographVehicleUse = {
    id?: string;
    startUtc?: string;
    endUtc?: string;
    start?: string;
    end?: string;
    registrationNumber?: string;
    vehicleRegistration?: string;
    vehicleRegistrationNumber?: string;
    vehicle_registration?: string;
    distanceKm?: number | null;
    startOdometerKm?: number | null;
    endOdometerKm?: number | null;
};

export type TachographViolation = {
    id?: string;
    code?: string;
    violationType: string;
    occurredAtUtc: string;
    periodEndUtc?: string | null;
    endUtc?: string | null;
    description?: string | null;
    recommendation?: string | null;
    actualDurationMinutes?: number | null;
    limitDurationMinutes?: number | null;
};

export type TachographTimelineDay = {
    date: string;
    label?: string;
};

type TachographTimelineProps = {
    activities: TachographActivity[];
    day?: string;
    days?: TachographTimelineDay[];
    label?: string;
    countryEntries?: TachographCountryEntry[];
    vehicleUses?: TachographVehicleUse[];
    violations?: TachographViolation[];
    cardReadings?: TachographCardReading[];
    onViolationClick?: (violationId: string) => void;
};

type DayRow = {
    date: string;
    label: string;
    dayName: string;
    shortDate: string;
};

type Segment = {
    id: string;
    type: ActivityKind;
    label: string;
    start: Date;
    end: Date;
    left: number;
    width: number;
    seconds: number;
    vehicleRegistration: string | null;
};

type Marker = {
    id: string;
    kind: "country" | "vehicle" | "violation" | "card";
    left: number;
    label: string;
    title: string;
    violationId?: string;
};

type ActivityKind = "DRIVING" | "REST" | "WORK" | "AVAILABILITY" | "UNKNOWN";

const secondsInDay = 24 * 60 * 60;
const hourLabels = Array.from({ length: 13 }, (_, index) => index * 2);
const gridHours = Array.from({ length: 25 }, (_, hour) => hour);

const activityMeta: Record<ActivityKind, { label: string; icon: string; className: string }> = {
    DRIVING: { label: "Jazda", icon: "▶", className: "driving" },
    REST: { label: "Odpoczynek", icon: "▮", className: "rest" },
    WORK: { label: "Inna praca", icon: "◆", className: "work" },
    AVAILABILITY: { label: "Dyspozycyjność", icon: "◌", className: "availability" },
    UNKNOWN: { label: "Brak danych", icon: "·", className: "unknown" },
};

const orderedActivityKinds: ActivityKind[] = ["DRIVING", "REST", "WORK", "AVAILABILITY", "UNKNOWN"];

const dayNameFormatter = new Intl.DateTimeFormat("pl-PL", { weekday: "short", timeZone: "UTC" });
const dateFormatter = new Intl.DateTimeFormat("pl-PL", { day: "2-digit", month: "2-digit", timeZone: "UTC" });
const fullDateFormatter = new Intl.DateTimeFormat("pl-PL", { day: "2-digit", month: "long", year: "numeric", timeZone: "UTC" });
const timeFormatter = new Intl.DateTimeFormat("pl-PL", { hour: "2-digit", minute: "2-digit", timeZone: "UTC" });
const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });

function parseDate(value?: string | null) {
    if (!value) return null;
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
}

function dayStart(day: string) {
    return new Date(`${day}T00:00:00Z`);
}

function addDays(date: Date, days: number) {
    return new Date(date.getTime() + days * secondsInDay * 1000);
}

function dayKey(date: Date) {
    const year = date.getUTCFullYear();
    const month = String(date.getUTCMonth() + 1).padStart(2, "0");
    const day = String(date.getUTCDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function normalizeDayName(value: string) {
    return value.replace(".", "").slice(0, 4);
}

function getActivityKind(activityType: string): ActivityKind {
    const normalized = activityType.trim().toUpperCase();
    if (normalized.includes("DRIVING") || normalized === "DRIVE") return "DRIVING";
    if (normalized.includes("REST") || normalized.includes("BREAK")) return "REST";
    if (normalized.includes("WORK") || normalized.includes("OTHER_WORK")) return "WORK";
    if (normalized.includes("AVAILABILITY") || normalized.includes("AVAILABLE")) return "AVAILABILITY";
    return "UNKNOWN";
}

function getVehicleRegistration(activity: TachographActivity) {
    return activity.vehicleRegistration ?? activity.vehicleRegistrationNumber ?? activity.registrationNumber ?? null;
}

function getVehicleUseRegistration(vehicleUse: TachographVehicleUse) {
    return vehicleUse.registrationNumber ?? vehicleUse.vehicleRegistration ?? vehicleUse.vehicleRegistrationNumber ?? vehicleUse.vehicle_registration ?? "Pojazd";
}

function getCountryTimestamp(entry: TachographCountryEntry) {
    return parseDate(entry.entryTimeUtc ?? entry.timeUtc ?? entry.timestampUtc ?? entry.timestamp ?? entry.occurredAtUtc);
}

function getCountryCode(entry: TachographCountryEntry) {
    return (entry.countryCode ?? entry.country_code ?? "").trim().toUpperCase();
}

function getCardReadingTimestamp(reading: TachographCardReading) {
    return parseDate(reading.readAtUtc ?? reading.timestampUtc ?? reading.timeUtc ?? reading.occurredAtUtc);
}

function getViolationEnd(violation: TachographViolation) {
    return parseDate(violation.periodEndUtc ?? violation.endUtc);
}

function percentFor(date: Date, start: Date) {
    return ((date.getTime() - start.getTime()) / 1000 / secondsInDay) * 100;
}

function clampPercent(value: number) {
    return Math.max(0, Math.min(100, value));
}

function formatHour(hour: number) {
    return String(hour).padStart(2, "0") + ":00";
}

function formatDuration(seconds: number) {
    const safe = Math.max(0, Math.round(seconds));
    const hours = Math.floor(safe / 3600);
    const minutes = Math.floor((safe % 3600) / 60);
    if (hours <= 0) return `${minutes} min`;
    if (minutes <= 0) return `${hours} godz.`;
    return `${hours} godz. ${minutes} min`;
}

function buildRows(propsDays: TachographTimelineDay[] | undefined, day: string | undefined, activities: TachographActivity[], violations: TachographViolation[]) {
    const map = new Map<string, string | undefined>();

    if (propsDays?.length) {
        for (const item of propsDays) map.set(item.date, item.label);
    } else if (day) {
        map.set(day, undefined);
    } else {
        for (const activity of activities) {
            const start = parseDate(activity.startUtc);
            const end = parseDate(activity.endUtc);
            if (!start || !end || end <= start) continue;
            let cursor = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), start.getUTCDate()));
            while (cursor < end) {
                map.set(dayKey(cursor), undefined);
                cursor = addDays(cursor, 1);
            }
        }
        for (const violation of violations) {
            const occurred = parseDate(violation.occurredAtUtc);
            if (occurred) map.set(dayKey(occurred), undefined);
        }
    }

    return Array.from(map.entries())
        .sort(([left], [right]) => left.localeCompare(right))
        .slice(0, propsDays?.length ? propsDays.length : 7)
        .map(([date, explicitLabel]): DayRow => {
            const start = dayStart(date);
            return {
                date,
                label: explicitLabel ?? fullDateFormatter.format(start),
                dayName: normalizeDayName(dayNameFormatter.format(start)),
                shortDate: dateFormatter.format(start),
            };
        });
}

function buildSegmentsForDay(activities: TachographActivity[], day: string): Segment[] {
    const start = dayStart(day);
    const end = addDays(start, 1);

    return activities
        .map((activity): Segment | null => {
            const activityStart = parseDate(activity.startUtc);
            const activityEnd = parseDate(activity.endUtc);
            if (!activityStart || !activityEnd || activityEnd <= start || activityStart >= end) return null;

            const segmentStart = activityStart < start ? start : activityStart;
            const segmentEnd = activityEnd > end ? end : activityEnd;
            const seconds = Math.max(0, (segmentEnd.getTime() - segmentStart.getTime()) / 1000);
            const type = getActivityKind(activity.activityType);

            return {
                id: activity.id,
                type,
                label: activityMeta[type].label,
                start: segmentStart,
                end: segmentEnd,
                left: clampPercent(percentFor(segmentStart, start)),
                width: Math.max(0.18, percentFor(segmentEnd, start) - percentFor(segmentStart, start)),
                seconds,
                vehicleRegistration: getVehicleRegistration(activity),
            };
        })
        .filter((segment): segment is Segment => segment !== null)
        .sort((left, right) => left.start.getTime() - right.start.getTime());
}

function buildMarkersForDay(day: string, countryEntries: TachographCountryEntry[], vehicleUses: TachographVehicleUse[], violations: TachographViolation[], cardReadings: TachographCardReading[]): Marker[] {
    const start = dayStart(day);
    const end = addDays(start, 1);
    const markers: Marker[] = [];

    for (const entry of countryEntries) {
        const timestamp = getCountryTimestamp(entry);
        const code = getCountryCode(entry);
        if (!timestamp || timestamp < start || timestamp >= end || !code) continue;
        markers.push({ id: entry.id ?? `country-${code}-${timestamp.toISOString()}`, kind: "country", left: clampPercent(percentFor(timestamp, start)), label: code, title: `Kraj: ${code}\nGodzina: ${timeFormatter.format(timestamp)}` });
    }

    for (const vehicleUse of vehicleUses) {
        const timestamp = parseDate(vehicleUse.startUtc ?? vehicleUse.start);
        if (!timestamp || timestamp < start || timestamp >= end) continue;
        const registration = getVehicleUseRegistration(vehicleUse);
        markers.push({ id: vehicleUse.id ?? `vehicle-${registration}-${timestamp.toISOString()}`, kind: "vehicle", left: clampPercent(percentFor(timestamp, start)), label: registration, title: `Pojazd: ${registration}\nStart: ${dateTimeFormatter.format(timestamp)}` });
    }

    for (const reading of cardReadings) {
        const timestamp = getCardReadingTimestamp(reading);
        if (!timestamp || timestamp < start || timestamp >= end) continue;
        markers.push({ id: reading.id ?? `card-${timestamp.toISOString()}`, kind: "card", left: clampPercent(percentFor(timestamp, start)), label: "K", title: `${reading.label ?? "Odczyt karty"}: ${dateTimeFormatter.format(timestamp)}` });
    }

    for (const violation of violations) {
        const timestamp = parseDate(violation.occurredAtUtc);
        if (!timestamp || timestamp < start || timestamp >= end) continue;
        const label = getComplianceRuleLabel(violation.violationType, violation.code);
        const endTime = getViolationEnd(violation);
        markers.push({
            id: violation.id ?? `violation-${timestamp.toISOString()}`,
            kind: "violation",
            left: clampPercent(percentFor(timestamp, start)),
            label: "!",
            title: `${label}\nStart: ${dateTimeFormatter.format(timestamp)}${endTime ? `\nKoniec: ${dateTimeFormatter.format(endTime)}` : ""}`,
            violationId: violation.id,
        });
    }

    return markers.sort((left, right) => left.left - right.left);
}

function buildTooltip(segment: Segment) {
    return [
        `Typ: ${segment.label}`,
        `Start: ${dateTimeFormatter.format(segment.start)}`,
        `Koniec: ${dateTimeFormatter.format(segment.end)}`,
        `Czas: ${formatDuration(segment.seconds)}`,
        segment.vehicleRegistration ? `Pojazd: ${segment.vehicleRegistration}` : null,
    ].filter(Boolean).join("\n");
}

function buildDayTotals(segments: Segment[]) {
    const totals = new Map<ActivityKind, number>();
    for (const segment of segments) totals.set(segment.type, (totals.get(segment.type) ?? 0) + segment.seconds);
    return totals;
}

function buildNavigatorSegments(rows: Array<{ row: DayRow; segments: Segment[] }>) {
    const rangeStart = rows[0] ? dayStart(rows[0].row.date) : null;
    const rangeEnd = rows.at(-1) ? addDays(dayStart(rows.at(-1)!.row.date), 1) : null;
    if (!rangeStart || !rangeEnd || rangeEnd <= rangeStart) return [];
    const durationMs = rangeEnd.getTime() - rangeStart.getTime();

    return rows.flatMap(({ segments }) => segments.map((segment) => ({
        id: segment.id + segment.start.toISOString(),
        className: activityMeta[segment.type].className,
        left: ((segment.start.getTime() - rangeStart.getTime()) / durationMs) * 100,
        width: Math.max(0.1, ((segment.end.getTime() - segment.start.getTime()) / durationMs) * 100),
    })));
}

export default function TachographTimeline({ activities, day, days, label, countryEntries = [], vehicleUses = [], violations = [], cardReadings = [], onViolationClick }: TachographTimelineProps) {
    const rows = useMemo(() => buildRows(days, day, activities, violations), [activities, day, days, violations]);
    const rowModels = useMemo(() => rows.map((row) => ({ row, segments: buildSegmentsForDay(activities, row.date), markers: buildMarkersForDay(row.date, countryEntries, vehicleUses, violations, cardReadings) })), [activities, cardReadings, countryEntries, rows, vehicleUses, violations]);
    const navigatorSegments = buildNavigatorSegments(rowModels);
    const dateRange = rows.length > 1 ? `${rows[0]?.shortDate ?? ""} - ${rows.at(-1)?.shortDate ?? ""}` : rows[0]?.label ?? label ?? "";

    if (rows.length === 0) {
        return <section className="tachograph-timeline-pro" aria-label="Oś czasu tachografu"><div className="tachograph-empty">Brak aktywności w wybranym zakresie</div></section>;
    }

    return (
        <section className="tachograph-timeline-pro" aria-label="Oś czasu tachografu">
            <header className="tachograph-pro-header">
                <div><span>Oś czasu tachografu</span><h4>{label ?? dateRange}</h4></div>
                <div className="tachograph-pro-meta"><strong>{dateRange}</strong><span>{rows.length === 1 ? "1 dzień" : `${rows.length} dni`}</span></div>
            </header>
            <div className="tachograph-pro-scroll">
                <div className="tachograph-pro-grid">
                    <div className="tachograph-time-scale" aria-hidden="true"><div /><div className="tachograph-time-axis">{hourLabels.map((hour) => <span key={hour} style={{ left: `${(hour / 24) * 100}%` }}>{formatHour(hour)}</span>)}</div></div>
                    <div className="tachograph-week-rows">
                        {rowModels.map(({ row, segments, markers }) => {
                            const totals = buildDayTotals(segments);
                            const drivingSeconds = totals.get("DRIVING") ?? 0;
                            return (
                                <div className="tachograph-day-row" key={row.date}>
                                    <div className="tachograph-day-label"><strong>{row.dayName}</strong><span>{row.shortDate}</span><small>Jazda {formatDuration(drivingSeconds)}</small></div>
                                    <div className="tachograph-day-track">
                                        <div className="tachograph-grid-lines" aria-hidden="true">{gridHours.map((hour) => <i className={hour % 4 === 0 ? "major" : undefined} key={hour} style={{ left: `${(hour / 24) * 100}%` }} />)}</div>
                                        <div className="tachograph-segments-row">
                                            {segments.length === 0 && <span className="tachograph-no-data">Brak danych</span>}
                                            {segments.map((segment, index) => (
                                                <span className={`tachograph-pro-segment ${activityMeta[segment.type].className}`} key={segment.id + segment.start.toISOString() + index} style={{ left: `${segment.left}%`, width: `${segment.width}%` }} title={buildTooltip(segment)} aria-label={buildTooltip(segment)}>
                                                    {segment.width >= 5.5 && <span>{formatDuration(segment.seconds)}</span>}
                                                </span>
                                            ))}
                                        </div>
                                        <div className="tachograph-marker-row">
                                            {markers.map((marker) => (
                                                <button className={`tachograph-pro-marker ${marker.kind}`} key={marker.id} style={{ left: `${marker.left}%` }} title={marker.title} type="button" onClick={() => marker.kind === "violation" && marker.violationId && onViolationClick?.(marker.violationId)}>{marker.label}</button>
                                            ))}
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
            <footer className="tachograph-pro-footer">
                <div className="tachograph-pro-legend" aria-label="Legenda aktywności">
                    {orderedActivityKinds.map((kind) => <span key={kind}><i className={activityMeta[kind].className}>{activityMeta[kind].icon}</i>{activityMeta[kind].label}</span>)}
                    <span><i className="country">PL</i>Kraj</span><span><i className="vehicle">V</i>Pojazd</span><span><i className="violation">!</i>Naruszenie</span>
                </div>
                <div className="tachograph-navigator" aria-label="Mini-przegląd zakresu">{navigatorSegments.map((segment) => <span key={segment.id} className={segment.className} style={{ left: `${segment.left}%`, width: `${segment.width}%` }} />)}</div>
            </footer>
        </section>
    );
}
