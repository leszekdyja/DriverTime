import { useMemo, useState } from "react";
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
    vehicle?: string | null;
    driverId?: string | null;
    driverName?: string | null;
    driverFirstName?: string | null;
    driverLastName?: string | null;
    firstName?: string | null;
    lastName?: string | null;
    driverCardNumber?: string | null;
    cardNumber?: string | null;
    distanceKm?: number | null;
    averageSpeedKmh?: number | null;
    countryStart?: string | null;
    countryEnd?: string | null;
    startCountryCode?: string | null;
    endCountryCode?: string | null;
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
    key: string;
    id: string;
    type: ActivityKind;
    label: string;
    sourceActivity: TachographActivity;
    start: Date;
    end: Date;
    left: number;
    width: number;
    seconds: number;
    vehicleRegistration: string | null;
    driverName: string | null;
    cardNumber: string | null;
    countryStart: string | null;
    countryEnd: string | null;
    distanceKm: number | null;
    averageSpeedKmh: number | null;
    vehicleUse: TachographVehicleUse | null;
    overlappingViolation: TachographViolation | null;
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
const breakResetSeconds = 45 * 60;
const dailyRestSeconds = 9 * 60 * 60;
const weeklyRestSeconds = 24 * 60 * 60;
const regularWeeklyRestSeconds = 45 * 60 * 60;
const hourLabels = Array.from({ length: 9 }, (_, index) => index * 3);
const gridHours = Array.from({ length: 25 }, (_, hour) => hour);

const activityMeta: Record<ActivityKind, { label: string; icon: string; className: string }> = {
    DRIVING: { label: "Jazda", icon: "▶", className: "driving" },
    REST: { label: "Odpoczynek", icon: "▮", className: "rest" },
    WORK: { label: "Praca", icon: "◆", className: "work" },
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
    return activity.vehicleRegistration ?? activity.vehicleRegistrationNumber ?? activity.registrationNumber ?? activity.vehicle ?? null;
}

function getCardNumber(activity: TachographActivity) {
    return activity.driverCardNumber ?? activity.cardNumber ?? null;
}

function getDriverName(activity: TachographActivity) {
    const directName = activity.driverName?.trim();
    if (directName) return directName;

    const firstName = (activity.driverFirstName ?? activity.firstName ?? "").trim();
    const lastName = (activity.driverLastName ?? activity.lastName ?? "").trim();
    const fullName = [firstName, lastName].filter(Boolean).join(" ");

    return fullName || null;
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

function getCountryType(entry: TachographCountryEntry) {
    const normalized = (entry.entryType ?? entry.type ?? "").trim().toUpperCase();
    if (normalized.includes("START") || normalized.includes("BEGIN") || normalized.includes("POCZ")) return "start";
    if (normalized.includes("END") || normalized.includes("STOP") || normalized.includes("KON")) return "end";
    return "unknown";
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

function formatNumber(value: number, fractionDigits = 0) {
    return new Intl.NumberFormat("pl-PL", { maximumFractionDigits: fractionDigits }).format(value);
}

function formatRangeLabel(rows: DayRow[]) {
    const firstRow = rows[0];
    const lastRow = rows.at(-1);

    if (!firstRow || !lastRow) return "";

    const firstDate = dayStart(firstRow.date);
    const lastDate = dayStart(lastRow.date);
    const sameMonth =
        firstDate.getUTCFullYear() === lastDate.getUTCFullYear() &&
        firstDate.getUTCMonth() === lastDate.getUTCMonth();

    if (sameMonth) {
        const monthAndYear = fullDateFormatter.format(lastDate).replace(/^\d+\s+/, "");
        return `${firstDate.getUTCDate()}?${lastDate.getUTCDate()} ${monthAndYear}`;
    }

    return `${fullDateFormatter.format(firstDate)} – ${fullDateFormatter.format(lastDate)}`;
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

function overlaps(start: Date, end: Date, otherStart: Date, otherEnd: Date) {
    return start < otherEnd && end > otherStart;
}

function findVehicleUse(segmentStart: Date, segmentEnd: Date, vehicleUses: TachographVehicleUse[], activityVehicle: string | null) {
    return vehicleUses.find((vehicleUse) => {
        const start = parseDate(vehicleUse.startUtc ?? vehicleUse.start);
        const end = parseDate(vehicleUse.endUtc ?? vehicleUse.end) ?? segmentEnd;
        if (!start || !overlaps(segmentStart, segmentEnd, start, end)) return false;
        const registration = getVehicleUseRegistration(vehicleUse);
        return !activityVehicle || registration === activityVehicle;
    }) ?? null;
}

function findCountryForSegment(segmentStart: Date, segmentEnd: Date, countryEntries: TachographCountryEntry[], mode: "start" | "end") {
    const entries = countryEntries
        .map((entry) => ({ entry, timestamp: getCountryTimestamp(entry), code: getCountryCode(entry), type: getCountryType(entry) }))
        .filter((item): item is { entry: TachographCountryEntry; timestamp: Date; code: string; type: string } => Boolean(item.timestamp && item.code))
        .sort((left, right) => left.timestamp.getTime() - right.timestamp.getTime());

    const preferred = entries.find((item) => item.timestamp >= segmentStart && item.timestamp <= segmentEnd && item.type === mode);
    if (preferred) return preferred.code;

    const reference = mode === "start" ? segmentStart : segmentEnd;
    const fallback = [...entries].reverse().find((item) => item.timestamp <= reference);
    return fallback?.code ?? null;
}

function findOverlappingViolation(segmentStart: Date, segmentEnd: Date, violations: TachographViolation[]) {
    return violations.find((violation) => {
        const start = parseDate(violation.occurredAtUtc);
        if (!start) return false;
        const end = getViolationEnd(violation) ?? start;
        return overlaps(segmentStart, segmentEnd, start, end.getTime() === start.getTime() ? new Date(start.getTime() + 60_000) : end);
    }) ?? null;
}

function buildSegmentsForDay(activities: TachographActivity[], day: string, countryEntries: TachographCountryEntry[], vehicleUses: TachographVehicleUse[], violations: TachographViolation[]): Segment[] {
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
            const vehicleRegistration = getVehicleRegistration(activity);
            const vehicleUse = findVehicleUse(segmentStart, segmentEnd, vehicleUses, vehicleRegistration);
            const distanceKm = type === "DRIVING" ? activity.distanceKm ?? null : null;
            const averageSpeedKmh = type === "DRIVING" ? activity.averageSpeedKmh ?? (distanceKm && seconds > 0 ? distanceKm / (seconds / 3600) : null) : null;
            const countryStart = activity.countryStart ?? activity.startCountryCode ?? findCountryForSegment(segmentStart, segmentEnd, countryEntries, "start");
            const countryEnd = activity.countryEnd ?? activity.endCountryCode ?? findCountryForSegment(segmentStart, segmentEnd, countryEntries, "end");

            return {
                key: `${activity.id}-${segmentStart.toISOString()}-${segmentEnd.toISOString()}`,
                id: activity.id,
                type,
                label: activityMeta[type].label,
                sourceActivity: activity,
                start: segmentStart,
                end: segmentEnd,
                left: clampPercent(percentFor(segmentStart, start)),
                width: Math.max(0.18, percentFor(segmentEnd, start) - percentFor(segmentStart, start)),
                seconds,
                vehicleRegistration: vehicleRegistration ?? (vehicleUse ? getVehicleUseRegistration(vehicleUse) : null),
                driverName: getDriverName(activity),
                cardNumber: getCardNumber(activity),
                countryStart,
                countryEnd,
                distanceKm,
                averageSpeedKmh,
                vehicleUse,
                overlappingViolation: findOverlappingViolation(segmentStart, segmentEnd, violations),
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
    return [`Typ: ${segment.label}`, `${timeFormatter.format(segment.start)}-${timeFormatter.format(segment.end)}`, `Czas: ${formatDuration(segment.seconds)}`].join("\n");
}

function buildDayTotals(segments: Segment[]) {
    const totals = new Map<ActivityKind, number>();
    for (const segment of segments) totals.set(segment.type, (totals.get(segment.type) ?? 0) + segment.seconds);
    return totals;
}

function calculateDrivingSinceLastBreak(activities: TachographActivity[], selectedSegment: Segment) {
    let counterSeconds = 0;
    const ordered = activities
        .map((activity) => ({ activity, start: parseDate(activity.startUtc), end: parseDate(activity.endUtc), type: getActivityKind(activity.activityType) }))
        .filter((item): item is { activity: TachographActivity; start: Date; end: Date; type: ActivityKind } => Boolean(item.start && item.end && item.end > item.start && item.start <= selectedSegment.end))
        .sort((left, right) => left.start.getTime() - right.start.getTime());

    for (const item of ordered) {
        const clippedEnd = item.end > selectedSegment.end ? selectedSegment.end : item.end;
        if (clippedEnd <= item.start) continue;
        const seconds = (clippedEnd.getTime() - item.start.getTime()) / 1000;

        if ((item.type === "REST" || item.type === "AVAILABILITY") && seconds >= breakResetSeconds) {
            counterSeconds = 0;
            continue;
        }

        if (item.type === "DRIVING") {
            counterSeconds += seconds;
        }
    }

    return counterSeconds;
}

function getSegmentRows(segment: Segment) {
    const rows: Array<[string, string]> = [
        ["Rozpoczęcie", dateTimeFormatter.format(segment.start)],
        ["Zakończenie", dateTimeFormatter.format(segment.end)],
        ["Czas trwania", formatDuration(segment.seconds)],
        ["Typ aktywności", segment.label],
    ];

    rows.push(["Kierowca", segment.driverName || "Brak danych"]);

    if (segment.vehicleRegistration) rows.push(["Pojazd", segment.vehicleRegistration]);
    if (segment.countryStart) rows.push(["Kraj rozpoczęcia", segment.countryStart]);
    if (segment.countryEnd) rows.push(["Kraj zakończenia", segment.countryEnd]);
    if (segment.cardNumber) rows.push(["Numer karty", segment.cardNumber]);
    if (segment.type === "DRIVING" && segment.distanceKm !== null && segment.distanceKm !== undefined) rows.push(["Odległość", `${formatNumber(segment.distanceKm, 1)} km`]);
    if (segment.type === "DRIVING" && segment.averageSpeedKmh !== null && segment.averageSpeedKmh !== undefined && Number.isFinite(segment.averageSpeedKmh)) rows.push(["Średnia prędkość", `${formatNumber(segment.averageSpeedKmh, 1)} km/h`]);

    return rows;
}

function getRestAnalysisRows(segment: Segment) {
    if (segment.type !== "REST") return [];

    const rows: Array<[string, string]> = [["Długość odpoczynku", formatDuration(segment.seconds)]];
    if (segment.seconds >= dailyRestSeconds) rows.push(["Zaliczony jako dzienny", "Tak"]);
    if (segment.seconds >= weeklyRestSeconds) rows.push(["Zaliczony jako tygodniowy", segment.seconds >= regularWeeklyRestSeconds ? "Regularny" : "Skrócony"]);
    if (segment.seconds >= weeklyRestSeconds && segment.seconds < regularWeeklyRestSeconds) rows.push(["Skrócony", "Tak"]);

    return rows;
}

export default function TachographTimeline({ activities, day, days, label, countryEntries = [], vehicleUses = [], violations = [], cardReadings = [], onViolationClick }: TachographTimelineProps) {
    const [selectedSegmentKey, setSelectedSegmentKey] = useState<string | null>(null);
    const rows = useMemo(() => buildRows(days, day, activities, violations), [activities, day, days, violations]);
    const rowModels = useMemo(() => rows.map((row) => ({ row, segments: buildSegmentsForDay(activities, row.date, countryEntries, vehicleUses, violations), markers: buildMarkersForDay(row.date, countryEntries, vehicleUses, violations, cardReadings) })), [activities, cardReadings, countryEntries, rows, vehicleUses, violations]);

    const selectedSegment = rowModels.flatMap((model) => model.segments).find((segment) => segment.key === selectedSegmentKey) ?? null;
    const dateRange = rows.length > 1 ? formatRangeLabel(rows) : rows[0]?.label ?? label ?? "";


    if (rows.length === 0) {
        return <section className="tachograph-timeline-pro" aria-label="Oś czasu tachografu"><div className="tachograph-empty">Brak aktywności w wybranym zakresie</div></section>;
    }

    return (
        <section className="tachograph-timeline-pro" aria-label="Oś czasu tachografu">
            <header className="tachograph-pro-header">
                <div><span>Oś czasu tachografu</span><h4>{label ?? dateRange}</h4></div>
                <div className="tachograph-pro-meta"><strong>{dateRange}</strong><span>{rows.length === 1 ? "1 dzień" : `${rows.length} dni`}</span></div>
            </header>
            <div className="tachograph-analysis-layout">
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
                                            <div className="tachograph-grid-lines" aria-hidden="true">{gridHours.map((hour) => <i className={hour % 6 === 0 ? "major" : undefined} key={hour} style={{ left: `${(hour / 24) * 100}%` }} />)}</div>
                                            <div className="tachograph-segments-row">
                                                {segments.length === 0 && <span className="tachograph-no-data">Brak danych</span>}
                                                {segments.map((segment) => (
                                                    <button
                                                        className={`tachograph-pro-segment ${activityMeta[segment.type].className}${selectedSegmentKey === segment.key ? " selected" : ""}`}
                                                        key={segment.key}
                                                        style={{ left: `${segment.left}%`, width: `${segment.width}%` }}
                                                        title={buildTooltip(segment)}
                                                        aria-label={buildTooltip(segment)}
                                                        type="button"
                                                        onClick={() => setSelectedSegmentKey(segment.key)}
                                                    >
                                                        {segment.width >= 5.5 && <span>{formatDuration(segment.seconds)}</span>}
                                                    </button>
                                                ))}
                                            </div>
                                            <div className="tachograph-marker-row">
                                                {markers.map((marker) => (
                                                    <button className={`tachograph-pro-marker ${marker.kind}`} key={marker.id} style={{ left: `${marker.left}%` }} title={marker.title} aria-label={marker.title} type="button" onClick={() => marker.kind === "violation" && marker.violationId && onViolationClick?.(marker.violationId)}>{marker.label}</button>
                                                ))}
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                </div>
                <aside className="tachograph-segment-panel" aria-live="polite">
                    {selectedSegment ? (
                        <>
                            <span>Analiza segmentu</span>
                            <h5>{selectedSegment.label}</h5>
                            <dl>
                                {getSegmentRows(selectedSegment).map(([rowLabel, value]) => <div key={rowLabel}><dt>{rowLabel}</dt><dd>{value}</dd></div>)}
                                {selectedSegment.type === "DRIVING" && <div><dt>Czas jazdy od ostatniej przerwy</dt><dd>{formatDuration(calculateDrivingSinceLastBreak(activities, selectedSegment))}</dd></div>}
                                {selectedSegment.type === "DRIVING" && <div><dt>Udział w analizie naruszenia</dt><dd>{selectedSegment.overlappingViolation ? "Tak" : "Brak danych"}</dd></div>}
                                {getRestAnalysisRows(selectedSegment).map(([rowLabel, value]) => <div key={rowLabel}><dt>{rowLabel}</dt><dd>{value}</dd></div>)}
                            </dl>
                            {selectedSegment.type === "DRIVING" && selectedSegment.overlappingViolation?.id && onViolationClick && (
                                <button type="button" className="tachograph-rule-button" onClick={() => onViolationClick(selectedSegment.overlappingViolation!.id!)}>
                                    Pokaż analizę reguły
                                </button>
                            )}
                        </>
                    ) : (
                        <div className="tachograph-segment-panel-empty">
                            <span>Analiza segmentu</span>
                            <p>Kliknij segment aktywności, aby zobaczyć szczegóły.</p>
                        </div>
                    )}
                </aside>
            </div>
            <footer className="tachograph-pro-footer">
                <div className="tachograph-pro-legend" aria-label="Legenda aktywności">
                    {orderedActivityKinds.map((kind) => <span key={kind}><i className={activityMeta[kind].className}>{activityMeta[kind].icon}</i>{activityMeta[kind].label}</span>)}
                    <span><i className="country">PL</i>Kraj</span><span><i className="vehicle">V</i>Pojazd</span><span><i className="violation">!</i>Naruszenie</span>
                </div>
            </footer>
        </section>
    );
}

