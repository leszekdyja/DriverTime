import { useEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
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
    actualValue?: string | number | null;
    requiredValue?: string | number | null;
    excessValue?: string | number | null;
    missingValue?: string | number | null;
    actualValueMinutes?: number | null;
    requiredValueMinutes?: number | null;
    excessMinutes?: number | null;
    missingMinutes?: number | null;
    compensationMinutes?: number | null;
    compensationDeadlineUtc?: string | null;
    businessSummary?: string | null;
    scaleLabel?: string | null;
    actualDurationMinutes?: number | null;
    limitDurationMinutes?: number | null;
};

type TachographTimelineProps = {
    activities: TachographActivity[];
    day: string;
    label?: string;
    countryEntries?: TachographCountryEntry[];
    vehicleUses?: TachographVehicleUse[];
    violations?: TachographViolation[];
};

type TimelineSegment = {
    id: string;
    activityType: string;
    startUtc: Date;
    endUtc: Date;
    leftPercent: number;
    widthPercent: number;
    durationSeconds: number;
    vehicleRegistration: string | null;
    countryLabel: string | null;
};

type CountryMarker = {
    id: string;
    countryCode: string;
    countryName: string | null;
    timestamp: Date;
    leftPercent: number;
};

type VehicleMarker = {
    id: string;
    registrationNumber: string;
    startUtc: Date;
    endUtc: Date | null;
    leftPercent: number;
};

type ViolationMarker = {
    id: string;
    label: string;
    occurredAtUtc: Date;
    endUtc: Date | null;
    description: string | null;
    leftPercent: number;
    rangeLeftPercent: number;
    rangeWidthPercent: number;
    recommendation: string | null;
    actualValue: string | number | null;
    requiredValue: string | number | null;
    excessValue: string | number | null;
    missingValue: string | number | null;
    actualDurationMinutes: number | null;
    limitDurationMinutes: number | null;
};

const secondsInDay = 24 * 60 * 60;
const hourMarkers = Array.from({ length: 25 }, (_, hour) => hour);

const activityLabels: Record<string, string> = {
    DRIVING: "Jazda",
    WORK: "Praca",
    AVAILABILITY: "Dyspozycyjność",
    REST: "Odpoczynek",
};

const activityLegend = [
    { type: "DRIVING", label: activityLabels.DRIVING },
    { type: "WORK", label: activityLabels.WORK },
    { type: "AVAILABILITY", label: activityLabels.AVAILABILITY },
    { type: "REST", label: activityLabels.REST },
];

const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC",
});

const timeFormatter = new Intl.DateTimeFormat("pl-PL", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
});

function getDayStart(day: string) {
    return new Date(`${day}T00:00:00Z`);
}

function getDayBounds(day: string) {
    const dayStart = getDayStart(day);
    const dayStartMs = dayStart.getTime();

    if (Number.isNaN(dayStartMs)) {
        return null;
    }

    return {
        dayStart,
        dayEnd: new Date(dayStartMs + secondsInDay * 1000),
        dayStartMs,
    };
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

function getPercentForDate(date: Date, dayStartMs: number) {
    return ((date.getTime() - dayStartMs) / 1000 / secondsInDay) * 100;
}

function formatHour(hour: number) {
    return String(hour).padStart(2, "0");
}

function formatDuration(seconds: number) {
    const safeSeconds = Math.max(0, Math.round(seconds));
    const hours = Math.floor(safeSeconds / 3600);
    const minutes = Math.floor((safeSeconds % 3600) / 60);

    if (hours === 0) {
        return `${minutes} min`;
    }

    return `${hours} godz. ${minutes} min`;
}

function parseDate(value?: string | null) {
    if (!value) return null;

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
}

function getVehicleRegistration(activity: TachographActivity) {
    return activity.vehicleRegistration
        ?? activity.vehicleRegistrationNumber
        ?? activity.registrationNumber
        ?? null;
}

function getCountryEntryTimestamp(entry: TachographCountryEntry) {
    return parseDate(entry.entryTimeUtc ?? entry.timeUtc ?? entry.timestampUtc ?? entry.timestamp ?? entry.occurredAtUtc);
}

function getCountryEntryCode(entry: TachographCountryEntry) {
    return (entry.countryCode ?? entry.country_code ?? "").trim().toUpperCase();
}

function formatCountryLabel(code: string, name?: string | null) {
    const safeCode = code.trim().toUpperCase();
    const safeName = name?.trim();

    if (!safeCode) {
        return safeName || null;
    }

    return safeName ? `${safeCode} - ${safeName}` : safeCode;
}

function findCountryForActivity(
    segmentStart: Date,
    segmentEnd: Date,
    countryEntries: TachographCountryEntry[],
) {
    const matchingEntries = countryEntries
        .map((entry, index) => ({
            code: getCountryEntryCode(entry),
            name: entry.countryName ?? entry.country_name ?? null,
            timestamp: getCountryEntryTimestamp(entry),
            index,
        }))
        .filter((entry) => entry.code && entry.timestamp && entry.timestamp <= segmentEnd)
        .sort((left, right) => {
            const timeDiff = right.timestamp!.getTime() - left.timestamp!.getTime();

            return timeDiff !== 0 ? timeDiff : right.index - left.index;
        });

    const nearestBeforeOrDuring = matchingEntries.find((entry) => entry.timestamp! <= segmentStart);
    const nearestWithinSegment = matchingEntries.find(
        (entry) => entry.timestamp! >= segmentStart && entry.timestamp! <= segmentEnd,
    );
    const selectedEntry = nearestWithinSegment ?? nearestBeforeOrDuring;

    return selectedEntry
        ? formatCountryLabel(selectedEntry.code, selectedEntry.name)
        : null;
}
function formatActivityTooltip(segment: TimelineSegment) {
    const lines = [
        getActivityLabel(segment.activityType),
        `Start: ${timeFormatter.format(segment.startUtc)}`,
        `Koniec: ${timeFormatter.format(segment.endUtc)}`,
        `Czas trwania: ${formatDuration(segment.durationSeconds)}`,
    ];

    if (segment.vehicleRegistration) {
        lines.push(`Pojazd: ${segment.vehicleRegistration}`);
    }

    if (segment.countryLabel) {
        lines.push(`Kraj: ${segment.countryLabel}`);
    }

    return lines.join("\n");
}

function formatCountryTooltip(marker: CountryMarker) {
    const country = formatCountryLabel(marker.countryCode, marker.countryName) ?? marker.countryCode;

    return [`Kraj: ${country}`, `Godzina: ${timeFormatter.format(marker.timestamp)}`].join("\n");
}

function formatVehicleTooltip(marker: VehicleMarker) {
    const lines = [
        `Pojazd: ${marker.registrationNumber}`,
        `Start: ${timeFormatter.format(marker.startUtc)}`,
    ];

    if (marker.endUtc) {
        lines.push(`Koniec: ${timeFormatter.format(marker.endUtc)}`);
    }

    return lines.join("\n");
}

function formatViolationTooltip(marker: ViolationMarker) {
    const lines = [
        `Naruszenie: ${marker.label}`,
        `Czas wystąpienia: ${timeFormatter.format(marker.occurredAtUtc)}`,
    ];

    if (marker.endUtc) {
        lines.push(`Zakres: ${timeFormatter.format(marker.occurredAtUtc)} - ${timeFormatter.format(marker.endUtc)}`);
    }

    if (marker.description) {
        lines.push(marker.description);
    }

    return lines.join("\n");
}

function formatDateTime(date: Date) {
    return dateTimeFormatter.format(date);
}

function formatViolationRange(marker: ViolationMarker) {
    return marker.endUtc
        ? `${formatDateTime(marker.occurredAtUtc)} - ${formatDateTime(marker.endUtc)}`
        : "Brak dokładnego zakresu";
}

function formatComplianceValue(value: string | number | null) {
    if (value === null || value === undefined || value === "") {
        return null;
    }

    return typeof value === "number"
        ? value.toLocaleString("pl-PL")
        : value;
}

function getViolationDetailRows(marker: ViolationMarker) {
    const rows: Array<[string, string]> = [];
    const actualValue = formatComplianceValue(marker.actualValue);
    const requiredValue = formatComplianceValue(marker.requiredValue);
    const excessValue = formatComplianceValue(marker.excessValue);
    const missingValue = formatComplianceValue(marker.missingValue);

    if (actualValue) rows.push(["Rzeczywista wartość", actualValue]);
    if (requiredValue) rows.push(["Wymagana wartość", requiredValue]);
    if (excessValue) rows.push(["Przekroczenie", excessValue]);
    if (missingValue) rows.push(["Niedobór", missingValue]);
    if (!actualValue && marker.actualDurationMinutes !== null) {
        rows.push(["Rzeczywisty czas", formatDuration(marker.actualDurationMinutes * 60)]);
    }
    if (!requiredValue && marker.limitDurationMinutes !== null) {
        rows.push(["Wymagany limit", formatDuration(marker.limitDurationMinutes * 60)]);
    }

    return rows;
}

function doesSegmentOverlapViolation(segment: TimelineSegment, marker: ViolationMarker) {
    if (!marker.endUtc) {
        return segment.startUtc <= marker.occurredAtUtc && segment.endUtc >= marker.occurredAtUtc;
    }

    return segment.startUtc < marker.endUtc && segment.endUtc > marker.occurredAtUtc;
}

function buildSegments(
    activities: TachographActivity[],
    day: string,
    countryEntries: TachographCountryEntry[],
): TimelineSegment[] {
    const bounds = getDayBounds(day);

    if (!bounds) {
        return [];
    }

    return activities
        .map((activity) => {
            const activityStart = parseDate(activity.startUtc);
            const activityEnd = parseDate(activity.endUtc);

            if (
                !activityStart ||
                !activityEnd ||
                activityEnd <= activityStart ||
                activityEnd <= bounds.dayStart ||
                activityStart >= bounds.dayEnd
            ) {
                return null;
            }

            const segmentStart = activityStart < bounds.dayStart ? bounds.dayStart : activityStart;
            const segmentEnd = activityEnd > bounds.dayEnd ? bounds.dayEnd : activityEnd;
            const durationSeconds = (segmentEnd.getTime() - segmentStart.getTime()) / 1000;

            if (durationSeconds <= 0) {
                return null;
            }

            return {
                id: activity.id,
                activityType: activity.activityType,
                startUtc: segmentStart,
                endUtc: segmentEnd,
                leftPercent: getPercentForDate(segmentStart, bounds.dayStartMs),
                widthPercent: (durationSeconds / secondsInDay) * 100,
                durationSeconds,
                vehicleRegistration: getVehicleRegistration(activity),
                countryLabel: findCountryForActivity(segmentStart, segmentEnd, countryEntries),
            };
        })
        .filter((segment): segment is TimelineSegment => segment !== null)
        .sort((left, right) => left.startUtc.getTime() - right.startUtc.getTime());
}

function buildCountryMarkers(entries: TachographCountryEntry[], day: string): CountryMarker[] {
    const bounds = getDayBounds(day);

    if (!bounds) {
        return [];
    }

    return entries
        .map((entry, index) => {
            const timestamp = getCountryEntryTimestamp(entry);
            const countryCode = getCountryEntryCode(entry);

            if (!timestamp || timestamp < bounds.dayStart || timestamp >= bounds.dayEnd || !countryCode) {
                return null;
            }

            return {
                id: entry.id ?? `${countryCode}-${timestamp.toISOString()}-${index}`,
                countryCode,
                countryName: entry.countryName ?? entry.country_name ?? null,
                timestamp,
                leftPercent: getPercentForDate(timestamp, bounds.dayStartMs),
            };
        })
        .filter((marker): marker is CountryMarker => marker !== null)
        .sort((left, right) => left.timestamp.getTime() - right.timestamp.getTime());
}

function buildVehicleMarkers(vehicleUses: TachographVehicleUse[], day: string): VehicleMarker[] {
    const bounds = getDayBounds(day);

    if (!bounds) {
        return [];
    }

    return vehicleUses
        .map((vehicleUse, index) => {
            const startUtc = parseDate(vehicleUse.startUtc ?? vehicleUse.start);
            const endUtc = parseDate(vehicleUse.endUtc ?? vehicleUse.end);
            const registrationNumber = vehicleUse.registrationNumber
                ?? vehicleUse.vehicleRegistration
                ?? vehicleUse.vehicleRegistrationNumber
                ?? vehicleUse.vehicle_registration
                ?? "";

            if (
                !startUtc ||
                !registrationNumber.trim() ||
                startUtc >= bounds.dayEnd ||
                (endUtc && endUtc <= bounds.dayStart)
            ) {
                return null;
            }

            const markerStart = startUtc < bounds.dayStart ? bounds.dayStart : startUtc;
            const markerEnd = endUtc && endUtc > bounds.dayEnd ? bounds.dayEnd : endUtc;

            return {
                id: vehicleUse.id ?? `${registrationNumber}-${markerStart.toISOString()}-${index}`,
                registrationNumber,
                startUtc: markerStart,
                endUtc: markerEnd,
                leftPercent: getPercentForDate(markerStart, bounds.dayStartMs),
            };
        })
        .filter((marker): marker is VehicleMarker => marker !== null)
        .sort((left, right) => left.startUtc.getTime() - right.startUtc.getTime());
}

function buildViolationMarkers(violations: TachographViolation[], day: string): ViolationMarker[] {
    const bounds = getDayBounds(day);

    if (!bounds) {
        return [];
    }

    return violations
        .map((violation, index) => {
            const occurredAtUtc = parseDate(violation.occurredAtUtc);
            const rawEndUtc = parseDate(violation.periodEndUtc ?? violation.endUtc);
            const endUtc = rawEndUtc && occurredAtUtc && rawEndUtc > occurredAtUtc ? rawEndUtc : null;
            const violationEnd = endUtc ?? occurredAtUtc;

            if (!occurredAtUtc || !violationEnd || violationEnd < bounds.dayStart || occurredAtUtc >= bounds.dayEnd) {
                return null;
            }

            const markerTime = occurredAtUtc < bounds.dayStart ? bounds.dayStart : occurredAtUtc;
            const rangeStart = occurredAtUtc < bounds.dayStart ? bounds.dayStart : occurredAtUtc;
            const rangeEnd = violationEnd > bounds.dayEnd ? bounds.dayEnd : violationEnd;
            const rangeWidthPercent = Math.max(
                0.35,
                ((rangeEnd.getTime() - rangeStart.getTime()) / 1000 / secondsInDay) * 100,
            );

            return {
                id: violation.id ?? `${violation.violationType}-${occurredAtUtc.toISOString()}-${index}`,
                label: getComplianceRuleLabel(violation.violationType, violation.code),
                occurredAtUtc,
                endUtc,
                description: violation.businessSummary ?? violation.description ?? null,
                leftPercent: getPercentForDate(markerTime, bounds.dayStartMs),
                rangeLeftPercent: getPercentForDate(rangeStart, bounds.dayStartMs),
                rangeWidthPercent,
                recommendation: violation.recommendation ?? null,
                actualValue: violation.actualValue ?? violation.actualValueMinutes ?? null,
                requiredValue: violation.requiredValue ?? violation.requiredValueMinutes ?? null,
                excessValue: violation.excessValue ?? violation.excessMinutes ?? null,
                missingValue: violation.missingValue ?? violation.missingMinutes ?? null,
                actualDurationMinutes: violation.actualDurationMinutes ?? null,
                limitDurationMinutes: violation.limitDurationMinutes ?? null,
            };
        })
        .filter((marker): marker is ViolationMarker => marker !== null)
        .sort((left, right) => left.occurredAtUtc.getTime() - right.occurredAtUtc.getTime());
}

export default function TachographTimeline({
    activities,
    day,
    label,
    countryEntries = [],
    vehicleUses = [],
    violations = [],
}: TachographTimelineProps) {
    const rootRef = useRef<HTMLDivElement | null>(null);
    const [activeViolationId, setActiveViolationId] = useState<string | null>(null);
    const segments = buildSegments(activities, day, countryEntries);
    const countryMarkers = buildCountryMarkers(countryEntries, day);
    const vehicleMarkers = buildVehicleMarkers(vehicleUses, day);
    const violationMarkers = buildViolationMarkers(violations, day);
    const activeViolation = violationMarkers.find((marker) => marker.id === activeViolationId) ?? null;
    const activeViolationRows = activeViolation ? getViolationDetailRows(activeViolation) : [];

    useEffect(() => {
        if (!activeViolationId || violationMarkers.some((marker) => marker.id === activeViolationId)) {
            return;
        }

        setActiveViolationId(null);
    }, [activeViolationId, violationMarkers]);

    useEffect(() => {
        if (!activeViolationId) {
            return;
        }

        function handlePointerDown(event: globalThis.PointerEvent) {
            const target = event.target as Element | null;

            if (!target?.closest(".tachograph-violation-marker, .tachograph-violation-panel")) {
                setActiveViolationId(null);
            }
        }

        function handleKeyDown(event: globalThis.KeyboardEvent) {
            if (event.key === "Escape") {
                setActiveViolationId(null);
            }
        }

        document.addEventListener("pointerdown", handlePointerDown);
        document.addEventListener("keydown", handleKeyDown);

        return () => {
            document.removeEventListener("pointerdown", handlePointerDown);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, [activeViolationId]);

    function toggleViolation(marker: ViolationMarker) {
        setActiveViolationId((currentId) => currentId === marker.id ? null : marker.id);
    }

    function handleTrackPointerDown(event: PointerEvent<HTMLDivElement>) {
        if ((event.target as HTMLElement).closest(".tachograph-violation-marker, .tachograph-violation-panel")) {
            return;
        }

        setActiveViolationId(null);
    }

    function handleViolationKeyDown(event: KeyboardEvent<HTMLButtonElement>, marker: ViolationMarker) {
        if (event.key === "Escape") {
            event.stopPropagation();
            setActiveViolationId(null);
            return;
        }

        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            event.stopPropagation();
            toggleViolation(marker);
        }
    }

    return (
        <div className={`tachograph-timeline${activeViolation ? " has-active-violation" : ""}`} ref={rootRef} aria-label={label ? `Wykres tachografowy ${label}` : "Wykres tachografowy"}>
            <div className="tachograph-timeline-header">
                {label && <h4>{label}</h4>}
                <div className="tachograph-legend" aria-label="Legenda aktywności">
                    {activityLegend.map((item) => (
                        <span className="tachograph-legend-item" key={item.type}>
                            <i className={`tachograph-legend-dot ${getActivityClass(item.type)}`} />
                            {item.label}
                        </span>
                    ))}
                    <span className="tachograph-legend-item"><i className="tachograph-marker-sample country" />Kraj</span>
                    <span className="tachograph-legend-item"><i className="tachograph-marker-sample vehicle" />Pojazd</span>
                    <span className="tachograph-legend-item"><i className="tachograph-marker-sample violation" />Naruszenie</span>
                </div>
            </div>

            <div className="tachograph-scroll">
                <div className="tachograph-track" role="img" aria-label="Oś aktywności od 00:00 do 24:00" onPointerDown={handleTrackPointerDown}>
                    <div className="tachograph-hour-grid" aria-hidden="true">
                        {hourMarkers.map((hour) => (
                            <span
                                className={hour % 6 === 0 ? "major" : undefined}
                                key={hour}
                                style={{ left: `${(hour / 24) * 100}%` }}
                            />
                        ))}
                    </div>

                    <div className="tachograph-marker-layer country-layer" aria-label="Znaczniki krajów">
                        {countryMarkers.map((marker) => (
                            <span
                                className="tachograph-country-marker"
                                data-tooltip={formatCountryTooltip(marker)}
                                key={marker.id}
                                style={{ left: `${marker.leftPercent}%` }}
                                tabIndex={0}
                                aria-label={formatCountryTooltip(marker)}
                            >
                                {marker.countryCode}
                            </span>
                        ))}
                    </div>

                    <div className="tachograph-marker-layer vehicle-layer" aria-label="Znaczniki pojazdów">
                        {vehicleMarkers.map((marker) => (
                            <span
                                className="tachograph-vehicle-marker"
                                data-tooltip={formatVehicleTooltip(marker)}
                                key={marker.id}
                                style={{ left: `${marker.leftPercent}%` }}
                                tabIndex={0}
                                aria-label={formatVehicleTooltip(marker)}
                            >
                                {marker.registrationNumber}
                            </span>
                        ))}
                    </div>

                    <div className="tachograph-activity-layer">
                        {segments.map((segment, index) => (
                            <span
                                className={`tachograph-segment ${getActivityClass(segment.activityType)}${activeViolation ? (doesSegmentOverlapViolation(segment, activeViolation) ? " in-active-violation" : " dimmed-by-violation") : ""}`}
                                key={`${segment.id}-${segment.startUtc.toISOString()}-${index}`}
                                style={{
                                    left: `${segment.leftPercent}%`,
                                    width: `${segment.widthPercent}%`,
                                }}
                                tabIndex={0}
                                aria-label={formatActivityTooltip(segment)}
                                data-tooltip={formatActivityTooltip(segment)}
                            />
                        ))}
                    </div>

                    <div className="tachograph-marker-layer violation-layer" aria-label="Znaczniki naruszeń">
                        {violationMarkers.map((marker) => (
                            <span
                                className={`tachograph-violation-range${activeViolationId === marker.id ? " active" : ""}`}
                                key={`${marker.id}-range`}
                                style={{
                                    left: `${marker.rangeLeftPercent}%`,
                                    width: `${marker.rangeWidthPercent}%`,
                                }}
                            />
                        ))}
                        {violationMarkers.map((marker) => (
                            <button
                                className={`tachograph-violation-marker${activeViolationId === marker.id ? " active" : ""}`}
                                data-tooltip={formatViolationTooltip(marker)}
                                aria-expanded={activeViolationId === marker.id}
                                key={marker.id}
                                style={{ left: `${marker.leftPercent}%` }}
                                type="button"
                                aria-label={formatViolationTooltip(marker)}
                                onClick={(event) => {
                                    event.stopPropagation();
                                    toggleViolation(marker);
                                }}
                                onKeyDown={(event) => handleViolationKeyDown(event, marker)}
                            >
                                {"⚠"}
                            </button>
                        ))}

                        {activeViolation && (
                            <aside
                                className="tachograph-violation-panel"
                                style={{ left: `${activeViolation.leftPercent}%` }}
                                role="dialog"
                                aria-label="Szczegóły naruszenia na osi czasu"
                                onPointerDown={(event) => event.stopPropagation()}
                            >
                                <div className="tachograph-violation-panel-header">
                                    <span>Naruszenie</span>
                                    <button
                                        type="button"
                                        aria-label="Zamknij szczegóły naruszenia"
                                        onClick={() => setActiveViolationId(null)}
                                    >
                                        x
                                    </button>
                                </div>
                                <h5>{activeViolation.label}</h5>
                                <dl className="tachograph-violation-panel-grid">
                                    <div>
                                        <dt>Czas wystąpienia</dt>
                                        <dd>{formatDateTime(activeViolation.occurredAtUtc)}</dd>
                                    </div>
                                    <div>
                                        <dt>Zakres</dt>
                                        <dd>{formatViolationRange(activeViolation)}</dd>
                                    </div>
                                    {activeViolationRows.map(([rowLabel, value]) => (
                                        <div key={rowLabel}>
                                            <dt>{rowLabel}</dt>
                                            <dd>{value}</dd>
                                        </div>
                                    ))}
                                </dl>
                                {activeViolation.description && <p>{activeViolation.description}</p>}
                                {activeViolation.recommendation && (
                                    <div className="tachograph-violation-recommendation">
                                        <strong>Rekomendacja</strong>
                                        <p>{activeViolation.recommendation}</p>
                                    </div>
                                )}
                            </aside>
                        )}
                    </div>
                </div>

                <div className="tachograph-hours" aria-hidden="true">
                    {hourMarkers.map((hour) => (
                        <span className={hour % 6 === 0 ? "major" : undefined} key={hour}>
                            {formatHour(hour)}
                        </span>
                    ))}
                </div>
            </div>
        </div>
    );
}