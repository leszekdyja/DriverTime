import { memo, useCallback, useEffect, useState, type FormEvent } from "react";

import { EmptyState } from "./UiStates";

import {
    getDriverActivityCalendar,
    type ActivityCalendarDay,
    type CalendarActivity,
    type DriverViolation,
} from "../services/driverActivityCalendarService";
import { getComplianceRuleLabel } from "../utils/complianceLabels";

const dayFormatter = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "2-digit",
    month: "long",
    year: "numeric",
    timeZone: "UTC",
});

const dateTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "UTC",
});

const activityLabels: Record<string, string> = {
    DRIVING: "Jazda",
    WORK: "Praca",
    REST: "Odpoczynek",
    AVAILABILITY: "Dyspozycyjnosc",
};

function toDateInputValue(date: Date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    return `${year}-${month}-${day}`;
}

function getDefaultRange() {
    const to = new Date();
    const from = new Date(to);
    from.setDate(from.getDate() - 6);
    return { from: toDateInputValue(from), to: toDateInputValue(to) };
}

function formatDuration(seconds: number) {
    const hours = Math.floor(Math.max(seconds, 0) / 3600);
    const minutes = Math.floor((Math.max(seconds, 0) % 3600) / 60);
    return `${hours} godz. ${minutes} min`;
}

function formatMinutes(minutes: number) {
    return formatDuration(minutes * 60);
}

function formatDateTime(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateTimeFormatter.format(date);
}

function getViolationMetadataString(violation: DriverViolation, key: string) {
    const value = violation.metadata?.[key];

    return typeof value === "string" && value.trim() ? value : null;
}

function getViolationMetadataNumber(violation: DriverViolation, key: string) {
    const value = violation.metadata?.[key];

    if (typeof value === "number" && Number.isFinite(value)) {
        return value;
    }

    if (typeof value === "string" && value.trim()) {
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : null;
    }

    return null;
}

function DailyRestViolationDetails({ violation }: { violation: DriverViolation }) {
    if (violation.code !== "DAILY_REST") {
        return null;
    }

    const windowStart = getViolationMetadataString(violation, "analysisWindowStartUtc");
    const windowEnd = getViolationMetadataString(violation, "analysisWindowEndUtc");
    const longestRestMinutes = getViolationMetadataNumber(violation, "longestRestMinutes");
    const requiredReducedRestMinutes = getViolationMetadataNumber(violation, "requiredReducedRestMinutes");

    if (!windowStart && !windowEnd && longestRestMinutes === null && requiredReducedRestMinutes === null) {
        return null;
    }

    return (
        <dl className="violation-metadata-details">
            <div>
                <dt>Analizowane okno 24h</dt>
                <dd>{formatDateTime(windowStart)} - {formatDateTime(windowEnd)}</dd>
            </div>
            <div>
                <dt>Najdłuższy ciągły odpoczynek</dt>
                <dd>{longestRestMinutes === null ? "Brak danych" : formatMinutes(longestRestMinutes)}</dd>
            </div>
            <div>
                <dt>Wymagane minimum</dt>
                <dd>{requiredReducedRestMinutes === null ? "9 godz. 0 min" : formatMinutes(requiredReducedRestMinutes)}</dd>
            </div>
        </dl>
    );
}

function formatDay(value: string) {
    return dayFormatter.format(new Date(`${value}T00:00:00Z`));
}

function getActivityClass(activityType: string) {
    const normalized = activityType.toUpperCase();
    return normalized in activityLabels ? normalized.toLowerCase() : "other";
}

function getActivityLabel(activityType: string) {
    return activityLabels[activityType.toUpperCase()] || activityType || "Inne";
}

function displayViolationType(violation: DriverViolation) {
    return getComplianceRuleLabel(violation.violationType, violation.code);
}

function getTimelineStyle(activity: CalendarActivity) {
    const start = new Date(activity.startUtc);
    const end = new Date(activity.endUtc);
    const startSeconds = start.getUTCHours() * 3600
        + start.getUTCMinutes() * 60
        + start.getUTCSeconds();
    const endSeconds = end.getUTCHours() * 3600
        + end.getUTCMinutes() * 60
        + end.getUTCSeconds();
    const duration = activity.durationSeconds;
    const effectiveEnd = endSeconds === 0 && duration > 0 ? 86400 : endSeconds;

    return {
        left: `${Math.max(0, startSeconds / 864)}%`,
        width: `${Math.max(0.2, (effectiveEnd - startSeconds) / 864)}%`,
    };
}

export default function DriverActivityCalendar({ driverId }: { driverId: string }) {
    const [initialRange] = useState(getDefaultRange);
    const [dateFrom, setDateFrom] = useState(initialRange.from);
    const [dateTo, setDateTo] = useState(initialRange.to);
    const [days, setDays] = useState<ActivityCalendarDay[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    const loadCalendar = useCallback(async (from: string, to: string) => {
        if (from > to) {
            setError("Data początkowa nie może być późniejsza niż data końcowa.");
            return;
        }

        setIsLoading(true);
        setError("");

        try {
            const calendar = await getDriverActivityCalendar(driverId, from, to);
            setDays(calendar.days);
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania kalendarza.",
            );
        } finally {
            setIsLoading(false);
        }
    }, [driverId]);

    useEffect(() => {
        void loadCalendar(initialRange.from, initialRange.to);
    }, [initialRange.from, initialRange.to, loadCalendar]);

    function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        void loadCalendar(dateFrom, dateTo);
    }

    const hasData = days.some((day) =>
        day.activities.length > 0 || day.violations.length > 0,
    );

    return (
        <section className="driver-details-section activity-calendar-section">
            <div className="activity-calendar-heading">
                <div>
                    <h3>Kalendarz aktywności</h3>
                    <p>Dobowy przebieg pracy i odpoczynku kierowcy.</p>
                </div>
                <form className="activity-calendar-filters" onSubmit={handleSubmit}>
                    <label>Od<input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} /></label>
                    <label>Do<input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} /></label>
                    <button type="submit" disabled={isLoading}>{isLoading ? "Ładowanie..." : "Pokaż"}</button>
                </form>
            </div>

            {error && <p className="activity-calendar-error" role="alert">{error}</p>}
            {isLoading && days.length === 0 && <CalendarSkeleton />}
            {!isLoading && !error && days.length === 0 && <EmptyState title="Brak dni" description="W wybranym zakresie nie ma danych kalendarza." />}
            {!isLoading && !error && days.length > 0 && !hasData && <EmptyState title="Brak aktywności" description="W wybranym zakresie nie zapisano aktywności ani naruszeń." />}

            {!error && days.length > 0 && (
                <div className={isLoading ? "activity-calendar-days is-refreshing" : "activity-calendar-days"} aria-busy={isLoading}>
                    {days.map((day) => <CalendarDayCard day={day} key={day.date} />)}
                </div>
            )}
        </section>
    );
}

const CalendarDayCard = memo(function CalendarDayCard({ day }: { day: ActivityCalendarDay }) {
    return (
        <article className="activity-day-card">
            <div className="activity-day-heading">
                <h4>{formatDay(day.date)}</h4>
                {day.violations.length > 0 && <span>{day.violations.length} naruszeń</span>}
            </div>

            <div className="activity-day-summary">
                <Summary label="Jazda" value={day.drivingSeconds} className="driving" />
                <Summary label="Praca" value={day.workSeconds} className="work" />
                <Summary label="Odpoczynek" value={day.restSeconds} className="rest" />
                <Summary label="Dyspozycyjnosc" value={day.availabilitySeconds} className="availability" />
            </div>

            <div className="activity-timeline" aria-label={`Timeline ${day.date}`}>
                {day.activities.map((activity, index) => (
                    <span
                        className={`activity-segment ${getActivityClass(activity.activityType)}`}
                        key={`${activity.id}-${activity.startUtc}-${index}`}
                        style={getTimelineStyle(activity)}
                        title={`${getActivityLabel(activity.activityType)}: ${formatDuration(activity.durationSeconds)}`}
                    />
                ))}
            </div>
            <div className="timeline-hours"><span>00:00</span><span>06:00</span><span>12:00</span><span>18:00</span><span>24:00</span></div>

            {day.violations.length > 0 && (
                <ul className="activity-day-violations">
                    {day.violations.map((violation, index) => (
                        <li key={`${violation.code}-${violation.occurredAtUtc}-${index}`}>
                            <span className={`severity-dot ${violation.severity.toLowerCase()}`} />
                            <div><strong>{displayViolationType(violation)}</strong><p>{violation.description}</p><DailyRestViolationDetails violation={violation} /></div>
                        </li>
                    ))}
                </ul>
            )}
        </article>
    );
});

const Summary = memo(function Summary({ label, value, className }: { label: string; value: number; className: string }) {
    return <div className={`activity-summary-item ${className}`}><span>{label}</span><strong>{formatDuration(value)}</strong></div>;
});

function CalendarSkeleton() {
    return (
        <div className="activity-calendar-skeleton" aria-busy="true" aria-label="Ładowanie kalendarza">
            {Array.from({ length: 3 }, (_, index) => <div className="ui-skeleton activity-day-skeleton" key={index} />)}
        </div>
    );
}
