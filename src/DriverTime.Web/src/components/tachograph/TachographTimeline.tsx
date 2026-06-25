import "./TachographTimeline.css";

export type TachographActivity = {
    id: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds?: number;
    vehicleRegistration?: string | null;
    registrationNumber?: string | null;
};

type TachographTimelineProps = {
    activities: TachographActivity[];
    day: string;
    label?: string;
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

const timeFormatter = new Intl.DateTimeFormat("pl-PL", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
});

function getDayStart(day: string) {
    return new Date(`${day}T00:00:00Z`);
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

function formatHour(hour: number) {
    return `${String(hour).padStart(2, "0")}:00`;
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

function formatTooltip(segment: TimelineSegment) {
    const lines = [
        getActivityLabel(segment.activityType),
        `Start: ${timeFormatter.format(segment.startUtc)}`,
        `Koniec: ${timeFormatter.format(segment.endUtc)}`,
        `Czas trwania: ${formatDuration(segment.durationSeconds)}`,
    ];

    if (segment.vehicleRegistration) {
        lines.push(`Pojazd: ${segment.vehicleRegistration}`);
    }

    return lines.join("\n");
}

function buildSegments(activities: TachographActivity[], day: string): TimelineSegment[] {
    const dayStart = getDayStart(day);
    const dayStartMs = dayStart.getTime();

    if (Number.isNaN(dayStartMs)) {
        return [];
    }

    const dayEnd = new Date(dayStartMs + secondsInDay * 1000);

    return activities
        .map((activity) => {
            const activityStart = new Date(activity.startUtc);
            const activityEnd = new Date(activity.endUtc);

            if (
                Number.isNaN(activityStart.getTime()) ||
                Number.isNaN(activityEnd.getTime()) ||
                activityEnd <= activityStart ||
                activityEnd <= dayStart ||
                activityStart >= dayEnd
            ) {
                return null;
            }

            const segmentStart = activityStart < dayStart ? dayStart : activityStart;
            const segmentEnd = activityEnd > dayEnd ? dayEnd : activityEnd;
            const durationSeconds = (segmentEnd.getTime() - segmentStart.getTime()) / 1000;

            if (durationSeconds <= 0) {
                return null;
            }

            return {
                id: activity.id,
                activityType: activity.activityType,
                startUtc: segmentStart,
                endUtc: segmentEnd,
                leftPercent: ((segmentStart.getTime() - dayStartMs) / 1000 / secondsInDay) * 100,
                widthPercent: (durationSeconds / secondsInDay) * 100,
                durationSeconds,
                vehicleRegistration: activity.vehicleRegistration ?? activity.registrationNumber ?? null,
            };
        })
        .filter((segment): segment is TimelineSegment => segment !== null)
        .sort((left, right) => left.startUtc.getTime() - right.startUtc.getTime());
}

export default function TachographTimeline({ activities, day, label }: TachographTimelineProps) {
    const segments = buildSegments(activities, day);

    return (
        <div className="tachograph-timeline" aria-label={label ? `Wykres tachografowy ${label}` : "Wykres tachografowy"}>
            <div className="tachograph-timeline-header">
                {label && <h4>{label}</h4>}
                <div className="tachograph-legend" aria-label="Legenda aktywności">
                    {activityLegend.map((item) => (
                        <span className="tachograph-legend-item" key={item.type}>
                            <i className={`tachograph-legend-dot ${getActivityClass(item.type)}`} />
                            {item.label}
                        </span>
                    ))}
                </div>
            </div>

            <div className="tachograph-track" role="img" aria-label="Oś aktywności od 00:00 do 24:00">
                <div className="tachograph-hour-grid" aria-hidden="true">
                    {hourMarkers.map((hour) => (
                        <span
                            className={hour % 6 === 0 ? "major" : undefined}
                            key={hour}
                            style={{ left: `${(hour / 24) * 100}%` }}
                        />
                    ))}
                </div>
                {segments.map((segment, index) => (
                    <span
                        className={`tachograph-segment ${getActivityClass(segment.activityType)}`}
                        key={`${segment.id}-${segment.startUtc.toISOString()}-${index}`}
                        style={{
                            left: `${segment.leftPercent}%`,
                            width: `${segment.widthPercent}%`,
                        }}
                        tabIndex={0}
                        aria-label={formatTooltip(segment)}
                        data-tooltip={formatTooltip(segment)}
                    />
                ))}
            </div>

            <div className="tachograph-hours" aria-hidden="true">
                {hourMarkers.map((hour) => (
                    <span className={hour % 6 === 0 ? "major" : undefined} key={hour}>
                        {formatHour(hour)}
                    </span>
                ))}
            </div>
        </div>
    );
}
