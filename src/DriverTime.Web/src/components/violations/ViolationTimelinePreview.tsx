type PreviewActivity = {
    id: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
};

type ViolationTimelinePreviewProps = {
    activities: PreviewActivity[];
    occurredAtUtc?: string | null;
    periodEndUtc?: string | null;
};

type PreviewSegment = {
    id: string;
    label: string;
    className: string;
    leftPercent: number;
    widthPercent: number;
};

const secondsInDay = 24 * 60 * 60;

const activityLabels: Record<string, string> = {
    DRIVING: "Jazda",
    WORK: "Praca",
    AVAILABILITY: "Dyspozycyjność",
    REST: "Odpoczynek",
    UNKNOWN: "Nieznane",
};

const legendItems = [
    { type: "driving", label: "Jazda" },
    { type: "work", label: "Praca" },
    { type: "availability", label: "Dyspozycyjność" },
    { type: "rest", label: "Odpoczynek" },
];

function parseDate(value?: string | null) {
    if (!value) return null;

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
}

function getUtcDayStart(date: Date) {
    return new Date(Date.UTC(
        date.getUTCFullYear(),
        date.getUTCMonth(),
        date.getUTCDate(),
    ));
}

function getUtcDayEnd(date: Date) {
    return new Date(getUtcDayStart(date).getTime() + secondsInDay * 1000);
}

function getActivityKind(activityType: string) {
    const normalized = activityType.trim().toUpperCase();

    if (normalized.includes("DRIVING") || normalized === "DRIVE") return "driving";
    if (normalized.includes("WORK") || normalized.includes("OTHER_WORK")) return "work";
    if (normalized.includes("AVAILABILITY") || normalized.includes("AVAILABLE")) return "availability";
    if (normalized.includes("REST") || normalized.includes("BREAK")) return "rest";

    return "unknown";
}

function getActivityLabel(activityType: string) {
    const normalized = activityType.trim().toUpperCase();

    if (normalized.includes("DRIVING") || normalized === "DRIVE") return activityLabels.DRIVING;
    if (normalized.includes("WORK") || normalized.includes("OTHER_WORK")) return activityLabels.WORK;
    if (normalized.includes("AVAILABILITY") || normalized.includes("AVAILABLE")) return activityLabels.AVAILABILITY;
    if (normalized.includes("REST") || normalized.includes("BREAK")) return activityLabels.REST;

    return activityLabels.UNKNOWN;
}

function getPercent(date: Date, rangeStart: Date, rangeDurationMs: number) {
    return ((date.getTime() - rangeStart.getTime()) / rangeDurationMs) * 100;
}

export default function ViolationTimelinePreview({
    activities,
    occurredAtUtc,
    periodEndUtc,
}: ViolationTimelinePreviewProps) {
    const violationStart = parseDate(occurredAtUtc);

    if (!violationStart || activities.length === 0) {
        return null;
    }

    const parsedViolationEnd = parseDate(periodEndUtc);
    const violationEnd = parsedViolationEnd && parsedViolationEnd > violationStart
        ? parsedViolationEnd
        : violationStart;
    const rangeStart = getUtcDayStart(violationStart);
    const rangeEnd = violationEnd > getUtcDayEnd(violationStart)
        ? getUtcDayEnd(violationEnd)
        : getUtcDayEnd(violationStart);
    const rangeDurationMs = rangeEnd.getTime() - rangeStart.getTime();

    if (rangeDurationMs <= 0) {
        return null;
    }

    const segments = activities
        .map((activity): PreviewSegment | null => {
            const activityStart = parseDate(activity.startUtc);
            const activityEnd = parseDate(activity.endUtc);

            if (!activityStart || !activityEnd || activityEnd <= rangeStart || activityStart >= rangeEnd) {
                return null;
            }

            const segmentStart = activityStart < rangeStart ? rangeStart : activityStart;
            const segmentEnd = activityEnd > rangeEnd ? rangeEnd : activityEnd;
            const widthPercent = Math.max(0.35, getPercent(segmentEnd, rangeStart, rangeDurationMs) - getPercent(segmentStart, rangeStart, rangeDurationMs));

            return {
                id: activity.id,
                label: getActivityLabel(activity.activityType),
                className: getActivityKind(activity.activityType),
                leftPercent: Math.max(0, Math.min(100, getPercent(segmentStart, rangeStart, rangeDurationMs))),
                widthPercent: Math.min(100, widthPercent),
            };
        })
        .filter((segment): segment is PreviewSegment => segment !== null);

    if (segments.length === 0 || violationStart < rangeStart || violationStart > rangeEnd) {
        return null;
    }

    const violationLeftPercent = Math.max(0, Math.min(100, getPercent(violationStart, rangeStart, rangeDurationMs)));

    return (
        <div className="dispatcher-event-context" aria-label="Kontekst zdarzenia">
            <span>Kontekst zdarzenia</span>
            <div className="violation-timeline-preview" aria-label="Mini oś czasu aktywności kierowcy">
                <div className="violation-timeline-preview-track">
                    {segments.map((segment) => (
                        <span
                            aria-label={segment.label}
                            className={`violation-timeline-preview-segment ${segment.className}`}
                            key={segment.id}
                            style={{
                                left: `${segment.leftPercent}%`,
                                width: `${segment.widthPercent}%`,
                            }}
                            title={segment.label}
                        />
                    ))}
                    <span
                        className="violation-timeline-preview-marker"
                        style={{ left: `${violationLeftPercent}%` }}
                    >
                        <span>▼</span>
                        <strong>Naruszenie</strong>
                    </span>
                </div>
            </div>
            <div className="violation-timeline-preview-legend" aria-label="Legenda aktywności">
                {legendItems.map((item) => (
                    <span key={item.type}>
                        <i className={item.type} aria-hidden="true" />
                        {item.label}
                    </span>
                ))}
            </div>
        </div>
    );
}
