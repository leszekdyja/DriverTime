import { useEffect, useMemo, useState, type FormEvent, type KeyboardEvent } from "react";
import { useSearchParams } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import TachographTimeline from "../components/tachograph/TachographTimeline";
import ViolationTimelinePreview from "../components/violations/ViolationTimelinePreview";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getDrivers,
    recalculateCompliance,
    type ComplianceDriver,
} from "../services/complianceService";
import { exportViolationsExcel } from "../services/excelExportService";
import { exportViolationsPdf } from "../services/pdfExportService";
import {
    getDriverActivitiesByCard,
    type DriverActivity as TimelineActivity,
} from "../services/driverActivitiesService";
import {
    getDriverViolations,
    type DriverViolation,
    type ViolationFilters,
} from "../services/violationsService";
import { getComplianceRuleLabel, getSeverityLabel } from "../utils/complianceLabels";
import { formatDriverNameOrFallback } from "../utils/driverName";
import "../styles/violations.css";

type SeverityLevel = "info" | "warning" | "critical";
type ViolationStatus = "open" | "in-review" | "resolved";
type AlertSeverity = "info" | "warning" | "serious" | "critical";
type DispatcherKpiTone = "info" | "required" | "warning" | "danger" | "compensation" | "deadline";
type DispatcherScaleTone = "low" | "medium" | "high" | "critical" | "warning" | "info";

type DispatcherKpi = {
    label: string;
    value: string;
    tone: DispatcherKpiTone;
};

type DispatcherScaleBadge = {
    label: string;
    tone: DispatcherScaleTone;
};

const readAlertsStorageKey = "drivertime.violationAlerts.read";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const shortDateFormatter = new Intl.DateTimeFormat("pl-PL", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
});

const severityLabels: Record<SeverityLevel, string> = {
    info: "Informacyjne",
    warning: "Ostrzeżenie",
    critical: "Krytyczne",
};

const statusLabels: Record<ViolationStatus, string> = {
    open: "Otwarte",
    "in-review": "W analizie",
    resolved: "Zamknięte",
};

const statusTones: Record<ViolationStatus, "warning" | "info" | "success"> = {
    open: "warning",
    "in-review": "info",
    resolved: "success",
};

const severityTones: Record<SeverityLevel, "info" | "warning" | "critical"> = {
    info: "info",
    warning: "warning",
    critical: "critical",
};

const alertSeverityLabels: Record<AlertSeverity, string> = {
    info: "Informacyjne",
    warning: "Ostrzeżenie",
    serious: "Ostrzeżenie",
    critical: "Krytyczne",
};

const dispatcherStatusLabels: Record<string, string> = {
    OK: "OK",
    WARNING: "Ostrzeżenie",
    HIGH_RISK: "Wysokie ryzyko",
    BLOCKED: "Blokada planowania",
};

type TimelineDay = {
    date: string;
    label: string;
};

const timelineDayFormatter = new Intl.DateTimeFormat("pl-PL", {
    weekday: "long",
    day: "2-digit",
    month: "long",
    year: "numeric",
    timeZone: "UTC",
});
const alertSeverityTones: Record<AlertSeverity, "info" | "warning" | "danger" | "critical"> = {
    info: "info",
    warning: "warning",
    serious: "danger",
    critical: "critical",
};

function parseUtcDate(value?: string | null) {
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

function addDays(date: Date, days: number) {
    return new Date(date.getTime() + days * 24 * 60 * 60 * 1000);
}

function toUtcDayKey(date: Date) {
    const year = date.getUTCFullYear();
    const month = String(date.getUTCMonth() + 1).padStart(2, "0");
    const day = String(date.getUTCDate()).padStart(2, "0");

    return `${year}-${month}-${day}`;
}

function getViolationPeriod(violation: DriverViolation) {
    const start = parseUtcDate(violation.occurredAtUtc);
    const rawEnd = parseUtcDate(violation.periodEndUtc);
    const end = start && rawEnd && rawEnd > start ? rawEnd : null;

    return {
        start,
        end,
        hasExactRange: Boolean(start && end),
    };
}

function buildViolationTimelineDays(violation: DriverViolation): TimelineDay[] {
    const { start, end } = getViolationPeriod(violation);

    if (!start) {
        return [];
    }

    const firstDay = getUtcDayStart(start);
    const lastInstant = end && end.getTime() > firstDay.getTime()
        ? new Date(end.getTime() - 1)
        : start;
    const lastDay = getUtcDayStart(lastInstant);
    const days: TimelineDay[] = [];

    for (let cursor = firstDay; cursor <= lastDay; cursor = addDays(cursor, 1)) {
        days.push({
            date: toUtcDayKey(cursor),
            label: timelineDayFormatter.format(cursor),
        });
    }

    return days;
}

function getViolationTimelineFetchRange(days: TimelineDay[]) {
    if (days.length === 0) {
        return null;
    }

    const firstDay = new Date(`${days[0].date}T00:00:00Z`);
    const lastDay = new Date(`${days[days.length - 1].date}T00:00:00Z`);

    if (Number.isNaN(firstDay.getTime()) || Number.isNaN(lastDay.getTime())) {
        return null;
    }

    return {
        from: firstDay.toISOString(),
        to: addDays(lastDay, 1).toISOString(),
    };
}
function loadReadAlertIds() {
    try {
        const value = window.localStorage.getItem(readAlertsStorageKey);

        return value ? (JSON.parse(value) as string[]) : [];
    } catch {
        return [];
    }
}

function saveReadAlertIds(ids: string[]) {
    window.localStorage.setItem(readAlertsStorageKey, JSON.stringify(ids));
}

function formatDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function formatShortDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : shortDateFormatter.format(date);
}

function formatDateForInput(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? ""
        : date.toISOString().slice(0, 10);
}

function formatMinutes(minutes: number) {
    const safeMinutes = Math.max(minutes, 0);
    const hours = Math.floor(safeMinutes / 60);
    const restMinutes = safeMinutes % 60;

    if (hours === 0) {
        return `${restMinutes} min`;
    }

    if (restMinutes === 0) {
        return `${hours} godz.`;
    }

    return `${hours} godz. ${restMinutes} min`;
}

function formatSignedMinutes(minutes: number) {
    if (minutes > 0) {
        return `+${formatMinutes(minutes)}`;
    }

    if (minutes < 0) {
        return `-${formatMinutes(Math.abs(minutes))}`;
    }

    return "0 min";
}

function displayDriver(violation: DriverViolation) {
    return formatDriverNameOrFallback(
        violation.driverFirstName,
        violation.driverLastName,
    );
}

function displayViolationType(violation: DriverViolation) {
    return getComplianceRuleLabel(violation.violationType, violation.code);
}

function normalizeSeverity(severity: string): SeverityLevel {
    const value = severity.trim().toLowerCase().replaceAll("_", " ");

    if (value === "critical" || value === "high" || value === "severe" || value === "very serious") {
        return "critical";
    }

    if (value === "warning" || value === "medium" || value === "serious") {
        return "warning";
    }

    return "info";
}

function normalizeStatus(status?: string): ViolationStatus {
    const value = status?.trim().toLowerCase().replaceAll("_", " ");

    if (value === "resolved" || value === "closed" || value === "zamknięte") {
        return "resolved";
    }

    if (value === "in review" || value === "in-review" || value === "review" || value === "w analizie") {
        return "in-review";
    }

    return "open";
}

function isWithinLastDay(value: string) {
    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
        return false;
    }

    return Date.now() - date.getTime() <= 24 * 60 * 60 * 1000;
}

function getAlertSeverity(violation: DriverViolation): AlertSeverity {
    const severity = normalizeSeverity(violation.severity);

    if (severity === "critical") {
        return "critical";
    }

    if (severity === "warning") {
        return "serious";
    }

    return isWithinLastDay(violation.occurredAtUtc) ? "warning" : "info";
}

function getViolationKey(violation: DriverViolation) {
    return [
        violation.driverCardNumber,
        violation.occurredAtUtc,
        violation.violationType,
        violation.code,
    ].join("|");
}

function getDispatcherStatusLabel(status?: string | null) {
    if (!status) {
        return "Brak statusu";
    }

    return dispatcherStatusLabels[status] || status;
}

function getDispatcherStatusIcon(status?: string | null) {
    switch (status) {
        case "OK":
            return "✓";
        case "WARNING":
            return "!";
        case "HIGH_RISK":
            return "⚠";
        case "BLOCKED":
            return "⛔";
        default:
            return "!";
    }
}

function formatDispatcherFlag(value: boolean, trueText: string, falseText: string) {
    return value ? trueText : falseText;
}

function getDispatcherReason(violation: DriverViolation) {
    if (violation.excessMinutes && violation.excessMinutes > 0) {
        return `+${formatMinutes(violation.excessMinutes)} ponad limit`;
    }

    if (violation.missingMinutes && violation.missingMinutes > 0) {
        return `Brakuje ${formatMinutes(violation.missingMinutes)} odpoczynku`;
    }

    if (violation.differenceMinutes && violation.differenceMinutes !== 0) {
        const prefix = violation.differenceMinutes > 0 ? "+" : "brakuje ";
        return violation.differenceMinutes > 0
            ? `${prefix}${formatMinutes(violation.differenceMinutes)} ponad wymaganą wartość`
            : `${prefix}${formatMinutes(Math.abs(violation.differenceMinutes))}`;
    }

    if (
        violation.actualValueMinutes !== null && violation.actualValueMinutes !== undefined &&
        violation.requiredValueMinutes !== null && violation.requiredValueMinutes !== undefined
    ) {
        return `Rzeczywista wartość: ${formatMinutes(violation.actualValueMinutes)} / wymagane: ${formatMinutes(violation.requiredValueMinutes)}`;
    }

    return "";
}

function hasValue(value: number | null | undefined): value is number {
    return value !== null && value !== undefined;
}

function getDispatcherKpis(violation: DriverViolation): DispatcherKpi[] {
    const kpis: DispatcherKpi[] = [];

    if (hasValue(violation.actualValueMinutes)) {
        kpis.push({ label: "Rzeczywiste", value: formatMinutes(violation.actualValueMinutes), tone: "info" });
    }

    if (hasValue(violation.requiredValueMinutes)) {
        kpis.push({ label: "Wymagane", value: formatMinutes(violation.requiredValueMinutes), tone: "required" });
    }

    if (hasValue(violation.differenceMinutes)) {
        kpis.push({ label: "Różnica", value: formatSignedMinutes(violation.differenceMinutes), tone: "warning" });
    }

    if (hasValue(violation.missingMinutes)) {
        kpis.push({ label: "Brakuje", value: formatMinutes(violation.missingMinutes), tone: "danger" });
    }

    if (hasValue(violation.excessMinutes)) {
        kpis.push({
            label: "Ponad limit",
            value: violation.excessMinutes > 0 ? `+${formatMinutes(violation.excessMinutes)}` : formatSignedMinutes(violation.excessMinutes),
            tone: "danger",
        });
    }

    if (hasValue(violation.compensationMinutes)) {
        kpis.push({ label: "Rekompensata", value: formatMinutes(violation.compensationMinutes), tone: "compensation" });
    }

    if (violation.compensationDeadlineUtc) {
        kpis.push({ label: "Termin", value: formatShortDate(violation.compensationDeadlineUtc), tone: "deadline" });
    }

    return kpis;
}

function getDispatcherScaleBadge(violation: DriverViolation): DispatcherScaleBadge | null {
    const rawLabel = violation.scaleLabel?.trim();

    if (!rawLabel) {
        return null;
    }

    const normalized = rawLabel
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .trim()
        .toLowerCase();

    if (["low", "niskie", "niska", "niewielkie", "niewielka", "male", "mala"].includes(normalized)) {
        return { label: "Niewielkie", tone: "low" };
    }

    if (["medium", "srednie", "srednia", "umiarkowane", "umiarkowana"].includes(normalized)) {
        return { label: "Średnie", tone: "medium" };
    }

    if (["high", "wysokie", "wysoka", "powazne", "powazna", "poważne", "poważna"].includes(normalized)) {
        return { label: "Poważne", tone: "high" };
    }

    if (["critical", "krytyczne", "krytyczna"].includes(normalized)) {
        return { label: "Krytyczne", tone: "critical" };
    }

    if (["warning", "ostrzezenie", "ostrzezenie", "ostrzeżenie"].includes(normalized)) {
        return { label: "Ostrzeżenie", tone: "warning" };
    }

    if (["info", "informacyjne", "informacyjna", "informacja"].includes(normalized)) {
        return { label: "Informacyjne", tone: "info" };
    }

    return { label: rawLabel, tone: "info" };
}

function isDataQualityViolation(violation: DriverViolation) {
    const searchable = [
        violation.code,
        violation.violationType,
        violation.description,
    ].join(" ").toLowerCase();

    return searchable.includes("country") || searchable.includes("kraju") || searchable.includes("danych");
}

function getDispatcherNextSteps(violation: DriverViolation) {
    const status = violation.dispatcherRecommendation?.status;
    const steps: string[] = [];

    if (status === "OK") {
        steps.push("Można kontynuować planowanie zgodnie z limitami.");
    } else if (status === "HIGH_RISK" || status === "BLOCKED") {
        steps.push(
            "Zabezpiecz odpoczynek lub przerwę przed kolejną trasą.",
            "Nie planuj kolejnego zlecenia bez weryfikacji limitów.",
        );
    } else if (status === "WARNING" && isDataQualityViolation(violation)) {
        steps.push(
            "Zweryfikuj dane z kierowcą.",
            "Sprawdź, czy import DDD jest kompletny.",
            "Odnotuj brakujące dane w dokumentacji.",
        );
    } else if (status === "WARNING") {
        steps.push(
            "Zweryfikuj naruszenie z kierowcą.",
            "Sprawdź dane źródłowe i timeline aktywności.",
        );
    }

    if (hasValue(violation.missingMinutes) && violation.missingMinutes > 0) {
        steps.push(`Uzupełnij brakujący czas: ${formatMinutes(violation.missingMinutes) }.`);
    }

    if (hasValue(violation.excessMinutes) && violation.excessMinutes > 0) {
        steps.push(`Uwzględnij przekroczenie limitu o ${formatMinutes(violation.excessMinutes) } w kolejnym planie pracy.`);
    }

    if (hasValue(violation.compensationMinutes) && violation.compensationMinutes > 0) {
        steps.push(`Zaplanuj rekompensatę: ${formatMinutes(violation.compensationMinutes) }.`);
    }

    if (violation.compensationDeadlineUtc) {
        steps.push(`Dopilnuj terminu rekompensaty: ${formatShortDate(violation.compensationDeadlineUtc)}.`);
    }

    if (isDataQualityViolation(violation)) {
        steps.push("Zweryfikuj kraj rozpoczęcia lub zakończenia pracy w danych tachografu.");
    }

    steps.push("Zweryfikuj dane źródłowe DDD, jeśli wynik wygląda nietypowo.");

    return Array.from(new Set(steps));
}

function getViolationScaleLabel(violation: DriverViolation) {
    const label = violation.scaleLabel?.trim();

    if (label) {
        return label;
    }

    if (violation.excessMinutes && violation.excessMinutes > 0) {
        return `+${formatMinutes(violation.excessMinutes)}`;
    }

    if (violation.missingMinutes && violation.missingMinutes > 0) {
        return `brakuje ${formatMinutes(violation.missingMinutes)}`;
    }

    if (violation.compensationMinutes && violation.compensationMinutes > 0) {
        return `rekompensata ${formatMinutes(violation.compensationMinutes)}`;
    }

    if (violation.compensationDeadlineUtc) {
        return `do ${formatDate(violation.compensationDeadlineUtc)}`;
    }

    return "-";
}

function getViolationDuration(violation: DriverViolation) {
    if (violation.actualDurationMinutes <= 0 && violation.limitDurationMinutes <= 0) {
        return "Brak danych";
    }

    if (violation.limitDurationMinutes <= 0) {
        return formatMinutes(violation.actualDurationMinutes);
    }

    const overrun = Math.max(
        violation.actualDurationMinutes - violation.limitDurationMinutes,
        0,
    );

    return overrun > 0
        ? `+${formatMinutes(overrun)} ponad limit`
        : `${formatMinutes(violation.actualDurationMinutes)} / limit ${formatMinutes(violation.limitDurationMinutes)}`;
}

function getViolationMetadata(violation: DriverViolation) {
    if (violation.metadata) {
        return violation.metadata;
    }

    if (!violation.metadataJson) {
        return {};
    }

    try {
        return JSON.parse(violation.metadataJson) as Record<string, number | string | null>;
    } catch {
        return {};
    }
}

function getMetadataNumber(
    metadata: Record<string, number | string | null>,
    key: string,
) {
    const value = metadata[key];

    if (typeof value === "number") {
        return value;
    }

    if (typeof value === "string" && value.trim()) {
        const parsed = Number(value);

        return Number.isFinite(parsed) ? parsed : null;
    }

    return null;
}

function getBusinessDetailsRows(violation: DriverViolation) {
    const metadata = getViolationMetadata(violation);
    const details = violation.businessDetails;
    const actualRestMinutes =
        details?.actualRestMinutes
        ?? getMetadataNumber(metadata, "actualRestMinutes")
        ?? getMetadataNumber(metadata, "longestRestMinutes")
        ?? getMetadataNumber(metadata, "restMinutes");
    const requiredRestMinutes =
        details?.requiredRestMinutes
        ?? getMetadataNumber(metadata, "requiredRestMinutes");
    const missingRestMinutes =
        details?.missingRestMinutes
        ?? getMetadataNumber(metadata, "missingRestMinutes");
    const reducedWeeklyRestMinutes =
        details?.reducedWeeklyRestMinutes
        ?? getMetadataNumber(metadata, "reducedRestMinutes");
    const compensationDebtMinutes =
        violation.compensationMinutes
        ?? details?.compensationDebtMinutes
        ?? getMetadataNumber(metadata, "compensationDebtMinutes");
    const compensationDeadlineUtc = violation.compensationDeadlineUtc ?? details?.compensationDeadlineUtc;
    const countryIssueMessage =
        details?.countryIssueMessage?.trim()
        || (typeof metadata.message === "string" ? metadata.message : "");
    const continuousDrivingMinutes =
        details?.continuousDrivingMinutes
        ?? getMetadataNumber(metadata, "continuousDrivingMinutes")
        ?? getMetadataNumber(metadata, "totalDrivingMinutes");
    const requiredBreakMinutes =
        details?.requiredBreakMinutes
        ?? getMetadataNumber(metadata, "requiredBreakMinutes");
    const receivedBreakMinutes =
        details?.receivedBreakMinutes
        ?? getMetadataNumber(metadata, "receivedBreakMinutes");
    const drivingLimitMinutes =
        details?.drivingLimitMinutes
        ?? getMetadataNumber(metadata, "limitMinutes");
    const drivingExceededMinutes =
        violation.excessMinutes
        ?? details?.drivingExceededMinutes
        ?? getMetadataNumber(metadata, "exceededMinutes");
    const breakType =
        details?.breakType?.trim()
        || (typeof metadata.breakType === "string" ? metadata.breakType : "");
    const rows: Array<[string, string]> = [];

    if (!details) {
        if (violation.actualValueMinutes !== null && violation.actualValueMinutes !== undefined) {
            rows.push(["Wartość rzeczywista", formatMinutes(violation.actualValueMinutes)]);
        }

        if (violation.requiredValueMinutes !== null && violation.requiredValueMinutes !== undefined) {
            rows.push(["Wartość wymagana", formatMinutes(violation.requiredValueMinutes)]);
        }

        if (violation.excessMinutes !== null && violation.excessMinutes !== undefined && violation.excessMinutes > 0) {
            rows.push(["Przekroczenie", formatMinutes(violation.excessMinutes)]);
        }

        if (violation.missingMinutes !== null && violation.missingMinutes !== undefined && violation.missingMinutes > 0) {
            rows.push(["Brakująca wartość", formatMinutes(violation.missingMinutes)]);
        }
    }

    if (actualRestMinutes !== null && actualRestMinutes !== undefined) {
        rows.push(["Rzeczywisty odpoczynek", formatMinutes(actualRestMinutes)]);
    }

    if (requiredRestMinutes !== null && requiredRestMinutes !== undefined) {
        rows.push(["Wymagane minimum", formatMinutes(requiredRestMinutes)]);
    }

    if (missingRestMinutes !== null && missingRestMinutes !== undefined && missingRestMinutes > 0) {
        rows.push(["Brakujący odpoczynek", formatMinutes(missingRestMinutes)]);
    }

    if (reducedWeeklyRestMinutes !== null && reducedWeeklyRestMinutes !== undefined) {
        rows.push(["Skrócony odpoczynek tygodniowy", formatMinutes(reducedWeeklyRestMinutes)]);
    }

    if (compensationDebtMinutes !== null && compensationDebtMinutes !== undefined) {
        rows.push(["Rekompensata do odebrania", formatMinutes(compensationDebtMinutes)]);
    }

    if (compensationDeadlineUtc) {
        rows.push(["Termin rekompensaty", formatDate(compensationDeadlineUtc)]);
    }

    if (countryIssueMessage) {
        rows.push(["Dane kraju", countryIssueMessage]);
    }

    if (continuousDrivingMinutes !== null && continuousDrivingMinutes !== undefined) {
        rows.push(["Rzeczywisty czas jazdy", formatMinutes(continuousDrivingMinutes)]);
    }

    if (drivingLimitMinutes !== null && drivingLimitMinutes !== undefined && drivingLimitMinutes > 0) {
        rows.push(["Limit jazdy", formatMinutes(drivingLimitMinutes)]);
    }

    if (drivingExceededMinutes !== null && drivingExceededMinutes !== undefined && drivingExceededMinutes > 0) {
        rows.push(["Przekroczenie limitu", formatMinutes(drivingExceededMinutes)]);
    }

    if (requiredBreakMinutes !== null && requiredBreakMinutes !== undefined && requiredBreakMinutes > 0) {
        rows.push(["Wymagana przerwa", `${formatMinutes(requiredBreakMinutes)} albo 15 + 30 min`]);
    }

    if (receivedBreakMinutes !== null && receivedBreakMinutes !== undefined && receivedBreakMinutes > 0) {
        rows.push(["Odebrana przerwa", formatMinutes(receivedBreakMinutes)]);
    }

    if (breakType) {
        rows.push(["Rodzaj problemu z przerwą", breakType]);
    }

    return {
        summary: violation.businessSummary?.trim() || details?.summary?.trim() || countryIssueMessage,
        rows,
    };
}

function getLegalExplanation(violation: DriverViolation) {
    const code = violation.code.toUpperCase();

    if (code.includes("CONTINUOUS_DRIVING")) {
        return "Po 4 godz. 30 min jazdy kierowca powinien odebrać przerwę 45 min albo poprawny podział 15 + 30 min. Brak przerwy zwiększa ryzyko naruszenia EU 561/AETR.";
    }

    if (code.includes("DAILY_DRIVING")) {
        return "Dzienny czas jazdy standardowo nie powinien przekraczać 9 godzin. Wydłużenie do 10 godzin jest możliwe tylko w ograniczonej liczbie dni tygodnia.";
    }

    if (code.includes("DAILY_REST")) {
        return "Odpoczynek dzienny musi zostać odebrany w odpowiednim oknie czasu. Zbyt krótki lub opóźniony odpoczynek wpływa na zgodność pracy kierowcy.";
    }

    if (code.includes("WEEKLY")) {
        return "Przepisy wymagają kontroli tygodniowych limitów jazdy oraz regularnych i skróconych odpoczynków tygodniowych wraz z rekompensatą.";
    }

    if (code.includes("FERRY") || code.includes("TRAIN")) {
        return "Odpoczynek na promie lub w pociągu może być przerywany tylko w ograniczonym zakresie. Dłuższe przerwanie wymaga weryfikacji dokumentów.";
    }

    return "Naruszenie wymaga analizy kontekstu pracy kierowcy, harmonogramu i danych źródłowych z importu DDD.";
}

function getRecommendedAction(violation: DriverViolation) {
    const severity = normalizeSeverity(violation.severity);

    if (severity === "critical") {
        return "Zweryfikuj dane źródłowe, skontaktuj się z kierowcą i zaplanuj korektę harmonogramu przed kolejną trasą.";
    }

    if (severity === "warning") {
        return "Sprawdź przebieg dnia pracy, przerwy oraz import DDD. Dodaj notatkę operacyjną, jeśli naruszenie ma uzasadnienie.";
    }

    return "Monitoruj trend i uwzględnij zdarzenie przy kolejnym planowaniu pracy kierowcy.";
}

export default function ViolationsPage() {
    const [searchParams] = useSearchParams();
    const driverIdFromQuery = searchParams.get("driverId") ?? "";
    const violationIdFromQuery = searchParams.get("violationId") ?? "";
    const [violations, setViolations] = useState<DriverViolation[]>([]);
    const [drivers, setDrivers] = useState<ComplianceDriver[]>([]);
    const [readAlertIds, setReadAlertIds] = useState<string[]>(() => loadReadAlertIds());
    const [selectedDriver, setSelectedDriver] = useState(driverIdFromQuery);
    const [selectedSeverity, setSelectedSeverity] = useState("");
    const [selectedStatus, setSelectedStatus] = useState("");
    const [violationTypeFilter, setViolationTypeFilter] = useState("");
    const [dateFrom, setDateFrom] = useState("");
    const [dateTo, setDateTo] = useState("");
    const [selectedViolation, setSelectedViolation] = useState<DriverViolation | null>(null);
    const [dismissedViolationIdFromQuery, setDismissedViolationIdFromQuery] = useState("");
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [isRecalculating, setIsRecalculating] = useState(false);
    const [recalculationMessage, setRecalculationMessage] = useState("");
    const [recalculationError, setRecalculationError] = useState("");
    const [error, setError] = useState("");
    const [filterError, setFilterError] = useState("");

    async function loadViolationData(filters?: ViolationFilters) {
        setIsLoading(true);
        setError("");

        try {
            setViolations(await getDriverViolations(filters));
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania naruszeń.",
            );
        } finally {
            setIsLoading(false);
        }
    }

    useEffect(() => {
        async function loadViolations() {
            setIsLoading(true);
            setError("");

            try {
                const [loadedDrivers, loadedViolations] = await Promise.all([
                    getDrivers(),
                    getDriverViolations(),
                ]);
                setDrivers(loadedDrivers);
                setViolations(loadedViolations);
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania naruszeń.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadViolations();
    }, []);

    useEffect(() => {
        if (driverIdFromQuery) {
            setSelectedDriver(driverIdFromQuery);
        }
    }, [driverIdFromQuery]);

    useEffect(() => {
        if (
            !violationIdFromQuery
            || selectedViolation
            || dismissedViolationIdFromQuery === violationIdFromQuery
        ) {
            return;
        }

        const matchingViolation = violations.find(
            (violation) => violation.id === violationIdFromQuery,
        );

        if (matchingViolation) {
            setSelectedViolation(matchingViolation);
        }
    }, [dismissedViolationIdFromQuery, selectedViolation, violationIdFromQuery, violations]);

    useEffect(() => {
        if (!selectedViolation) {
            return;
        }

        function handleEscape(event: globalThis.KeyboardEvent) {
            if (event.key === "Escape") {
                closeSelectedViolation();
            }
        }

        window.addEventListener("keydown", handleEscape);

        return () => window.removeEventListener("keydown", handleEscape);
    }, [selectedViolation]);

    const driverOptions = useMemo(() => {
        return drivers
            .map((driver) => {
                const name = formatDriverNameOrFallback(driver.firstName, driver.lastName);
                const cardNumber = driver.cardNumber || "Brak numeru karty";

                return [driver.id, `${name} (${cardNumber})`] as const;
            })
            .sort((left, right) =>
                left[1].localeCompare(right[1], "pl"),
            );
    }, [drivers]);

    const filteredViolations = useMemo(() => {
        if (filterError) return [];

        return violations.filter((violation) => {
            const occurredDate = formatDateForInput(violation.occurredAtUtc);
            const severity = normalizeSeverity(violation.severity);
            const status = normalizeStatus(violation.status);
            const violationType = displayViolationType(violation).trim().toLowerCase();
            const searchedViolationType = violationTypeFilter.trim().toLowerCase();

            if (selectedDriver && violation.driverId !== selectedDriver) {
                return false;
            }

            if (selectedSeverity && severity !== selectedSeverity) {
                return false;
            }

            if (selectedStatus && status !== selectedStatus) {
                return false;
            }

            if (searchedViolationType && !violationType.includes(searchedViolationType)) {
                return false;
            }

            if (dateFrom && occurredDate && occurredDate < dateFrom) {
                return false;
            }

            if (dateTo && occurredDate && occurredDate > dateTo) {
                return false;
            }

            return true;
        });
    }, [dateFrom, dateTo, filterError, selectedDriver, selectedSeverity, selectedStatus, violationTypeFilter, violations]);

    const summary = useMemo(() => {
        const result = {
            total: filteredViolations.length,
            info: 0,
            warning: 0,
            critical: 0,
        };

        for (const violation of filteredViolations) {
            const severity = normalizeSeverity(violation.severity);

            if (severity === "critical") {
                result.critical++;
            } else if (severity === "warning") {
                result.warning++;
            } else {
                result.info++;
            }
        }

        return result;
    }, [filteredViolations]);

    const violationAlerts = useMemo(() => {
        return violations
            .filter((violation) => {
                const severity = normalizeSeverity(violation.severity);

                return severity !== "info" || isWithinLastDay(violation.occurredAtUtc);
            })
            .sort((left, right) => {
                const priority: Record<AlertSeverity, number> = {
                    info: 0,
                    warning: 1,
                    serious: 2,
                    critical: 3,
                };
                const severityDiff =
                    priority[getAlertSeverity(right)] - priority[getAlertSeverity(left)];

                if (severityDiff !== 0) {
                    return severityDiff;
                }

                return new Date(right.occurredAtUtc).getTime() - new Date(left.occurredAtUtc).getTime();
            })
            .slice(0, 6);
    }, [violations]);

    const activeAlertsCount = useMemo(() => {
        return violationAlerts.filter(
            (violation) => !readAlertIds.includes(getViolationKey(violation)),
        ).length;
    }, [readAlertIds, violationAlerts]);



    function getCurrentViolationFilters(): ViolationFilters {
        return {
            driverId: selectedDriver,
            fromDate: dateFrom,
            toDate: dateTo,
            severity: selectedSeverity,
            type: violationTypeFilter,
        };
    }

    async function handleRecalculateViolations() {
        setIsRecalculating(true);
        setRecalculationMessage("");
        setRecalculationError("");
        setError("");

        try {
            const result = await recalculateCompliance();
            setRecalculationMessage(
                `Przeliczono naruszenia dla ${result.recalculatedDriversCount} kierowców. Usunięto stare: ${result.deletedViolationsCount}, aktualne: ${result.savedViolationsCount}.`,
            );
            await loadViolationData(getCurrentViolationFilters());
        } catch (recalculationFailure) {
            setRecalculationError(
                recalculationFailure instanceof Error
                    ? recalculationFailure.message
                    : "Nie udało się przeliczyć naruszeń.",
            );
        } finally {
            setIsRecalculating(false);
        }
    }

    async function handlePdfExport() {
        setIsGeneratingPdf(true);
        setError("");

        try {
            await exportViolationsPdf(filteredViolations);
        } catch {
            setError("Nie udało się wygenerować pliku PDF.");
        } finally {
            setIsGeneratingPdf(false);
        }
    }

    async function handleExcelExport() {
        setIsGeneratingExcel(true);
        setError("");

        try {
            await exportViolationsExcel(filteredViolations);
        } catch {
            setError("Nie udało się wygenerować pliku Excel.");
        } finally {
            setIsGeneratingExcel(false);
        }
    }

    function handleFilterSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (dateFrom && dateTo && dateFrom > dateTo) {
            setFilterError("Data początkowa nie może być późniejsza niż data końcowa.");
            return;
        }

        setFilterError("");
        void loadViolationData({
            driverId: selectedDriver,
            fromDate: dateFrom,
            toDate: dateTo,
            severity: selectedSeverity,
            type: violationTypeFilter,
        });
    }

    async function clearFilters() {
        setSelectedDriver("");
        setSelectedSeverity("");
        setSelectedStatus("");
        setViolationTypeFilter("");
        setDateFrom("");
        setDateTo("");
        setFilterError("");
        await loadViolationData();
    }

    function handleRowKeyDown(event: KeyboardEvent<HTMLTableRowElement>, violation: DriverViolation) {
        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            openViolationDetails(violation);
        }
    }

    function openViolationDetails(violation: DriverViolation) {
        setDismissedViolationIdFromQuery("");
        setSelectedViolation(violation);
    }

    function closeSelectedViolation() {
        if (violationIdFromQuery) {
            setDismissedViolationIdFromQuery(violationIdFromQuery);
        }

        setSelectedViolation(null);
    }

    function markAlertAsRead(violation: DriverViolation) {
        const key = getViolationKey(violation);

        setReadAlertIds((currentIds) => {
            if (currentIds.includes(key)) {
                return currentIds;
            }

            const nextIds = [...currentIds, key];
            saveReadAlertIds(nextIds);

            return nextIds;
        });
    }

    return (
        <div className="violations-page">
            <section className="violations-hero">
                <div>
                    <span className="violations-eyebrow">Zgodność</span>
                    <h2>Naruszenia czasu pracy kierowców</h2>
                    <p>
                        Monitoruj naruszenia wykryte na podstawie aktywności DDD,
                        filtruj je według kierowcy, wagi, statusu i zakresu dat.
                    </p>
                </div>
                {!isLoading && !error && (
                    <div className="violations-heading-actions">
                        <button
                            className="violations-recalculate-button"
                            type="button"
                            onClick={() => void handleRecalculateViolations()}
                            disabled={isRecalculating || isGeneratingPdf || isGeneratingExcel}
                        >
                            {isRecalculating ? "Przeliczanie..." : "Przelicz naruszenia"}
                        </button>
                        <button
                            className="violations-pdf-button"
                            type="button"
                            onClick={() => void handlePdfExport()}
                            disabled={filteredViolations.length === 0 || isGeneratingPdf || isGeneratingExcel || isRecalculating}
                        >
                            {isGeneratingPdf ? "Generowanie PDF..." : "Eksport PDF"}
                        </button>
                        <button
                            className="violations-export-button"
                            type="button"
                            onClick={() => void handleExcelExport()}
                            disabled={filteredViolations.length === 0 || isGeneratingPdf || isGeneratingExcel || isRecalculating}
                        >
                            {isGeneratingExcel ? "Generowanie Excel..." : "Eksport Excel"}
                        </button>
                    </div>
                )}
            </section>

            {(recalculationMessage || recalculationError) && (
                <div
                    className={`violations-recalculation-status ${recalculationError ? "error" : "success"}`}
                    role={recalculationError ? "alert" : "status"}
                >
                    {recalculationError || recalculationMessage}
                </div>
            )}


            <section className="violations-summary" aria-label="Podsumowanie naruszeń">
                <SummaryCard label="Po filtrach" value={summary.total} tone="total" description="Liczba widocznych naruszeń" />
                <SummaryCard label={severityLabels.info} value={summary.info} tone="info" description="Niższy priorytet" />
                <SummaryCard label={severityLabels.warning} value={summary.warning} tone="warning" description="Wymaga weryfikacji" />
                <SummaryCard label={severityLabels.critical} value={summary.critical} tone="critical" description="Wysokie ryzyko" />
            </section>

            <section className="violation-alerts-panel" aria-label="Alerty naruszeń">
                <div className="violation-alerts-heading">
                    <div>
                        <span>Alerty naruszeń</span>
                        <h3>{activeAlertsCount} aktywnych alertów</h3>
                        <p>Alerty są liczone lokalnie z aktualnej listy naruszeń.</p>
                    </div>
                </div>

                {isLoading && <TableSkeleton rows={3} columns={4} />}

                {!isLoading && error && (
                    <div className="violation-alerts-state" role="alert">
                        Nie udało się pobrać danych naruszeń.
                    </div>
                )}

                {!isLoading && !error && violationAlerts.length === 0 && (
                    <EmptyState
                        title="Brak aktywnych alertów"
                        description="Nie wykryto nowych ani poważnych naruszeń wymagających reakcji."
                    />
                )}

                {!isLoading && !error && violationAlerts.length > 0 && (
                    <div className="violation-alerts-list">
                        {violationAlerts.map((violation) => {
                            const key = getViolationKey(violation);
                            const alertSeverity = getAlertSeverity(violation);
                            const isRead = readAlertIds.includes(key);

                            return (
                                <article className={`violation-alert-card ${alertSeverity}${isRead ? " is-read" : ""}`} key={key}>
                                    <div>
                                        <span>{isWithinLastDay(violation.occurredAtUtc) ? "Nowe naruszenie" : "Poważne naruszenie"}</span>
                                        <strong>{displayDriver(violation)}</strong>
                                        <p>{violation.description || displayViolationType(violation)}</p>
                                        <small>{formatDate(violation.occurredAtUtc)}</small>
                                    </div>
                                    <div className="violation-alert-actions">
                                        <StatusBadge
                                            label={alertSeverityLabels[alertSeverity]}
                                            tone={alertSeverityTones[alertSeverity]}
                                        />
                                        <button
                                            type="button"
                                            onClick={() => markAlertAsRead(violation)}
                                            disabled={isRead}
                                        >
                                            {isRead ? "Przeczytane" : "Oznacz jako przeczytane"}
                                        </button>
                                    </div>
                                </article>
                            );
                        })}
                    </div>
                )}
            </section>

            <form className="violations-filters" onSubmit={handleFilterSubmit}>
                <label>
                    Kierowca
                    <select value={selectedDriver} onChange={(event) => setSelectedDriver(event.target.value)}>
                        <option value="">Wszyscy kierowcy</option>
                        {driverOptions.map(([driverId, label]) => (
                            <option key={driverId} value={driverId}>
                                {label}
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Waga naruszenia
                    <select value={selectedSeverity} onChange={(event) => setSelectedSeverity(event.target.value)}>
                        <option value="">Wszystkie wagi</option>
                        <option value="info">Informacyjne</option>
                        <option value="warning">Ostrzeżenie</option>
                        <option value="critical">Krytyczne</option>
                    </select>
                </label>
                <label>
                    Status
                    <select value={selectedStatus} onChange={(event) => setSelectedStatus(event.target.value)}>
                        <option value="">Wszystkie statusy</option>
                        <option value="open">Otwarte</option>
                        <option value="in-review">W analizie</option>
                        <option value="resolved">Zamknięte</option>
                    </select>
                </label>
                <label>
                    Typ naruszenia
                    <input
                        type="search"
                        value={violationTypeFilter}
                        onChange={(event) => setViolationTypeFilter(event.target.value)}
                        placeholder="np. kompensacja odpoczynku"
                    />
                </label>
                <label>
                    Data od
                    <input type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} />
                </label>
                <label>
                    Data do
                    <input type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} />
                </label>
                <div className="violations-filter-actions">
                    <button type="submit">Zastosuj</button>
                    <button type="button" onClick={clearFilters}>Wyczyść</button>
                </div>
            </form>

            {(error || filterError) && (
                <div className="violations-error" role="alert">
                    <strong>Nie można wyświetlić naruszeń</strong>
                    <span>{error || filterError}</span>
                </div>
            )}

            {isLoading && (
                <section className="violations-panel" aria-busy="true">
                    <TableSkeleton rows={7} columns={7} />
                </section>
            )}

            {!isLoading && !error && !filterError && filteredViolations.length === 0 && (
                <section className="violations-panel">
                    <EmptyState
                        title="Brak naruszeń dla wybranych filtrów"
                        description="Zmień kierowcę, wagę, status albo zakres dat. Po imporcie nowych plików DDD lista odświeży się na podstawie danych backendowych."
                    />
                </section>
            )}

            {!isLoading && !error && !filterError && filteredViolations.length > 0 && (
                <section className="violations-panel">
                    <div className="violations-panel-heading">
                        <div>
                            <span>Lista naruszeń</span>
                            <h3>{filteredViolations.length} naruszeń w tabeli</h3>
                        </div>
                    </div>
                    <div className="violations-table-wrapper">
                        <table className="violations-table">
                            <thead>
                                <tr>
                                    <th>Kierowca</th>
                                    <th>Numer karty</th>
                                    <th>Typ naruszenia</th>
                                    <th>Data</th>
                                    <th>Opis</th>
                                    <th>Skala</th>
                                    <th>Poziom</th>
                                    <th>Status</th>
                                    <th>Szczegóły</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredViolations.map((violation, index) => {
                                    const severity = normalizeSeverity(violation.severity);
                                    const status = normalizeStatus(violation.status);

                                    return (
                                        <tr
                                            className="violation-clickable-row"
                                            key={`${violation.driverCardNumber}-${violation.occurredAtUtc}-${violation.violationType}-${index}`}
                                            tabIndex={0}
                                            onClick={() => openViolationDetails(violation)}
                                            onKeyDown={(event) => handleRowKeyDown(event, violation)}
                                        >
                                            <td data-label="Kierowca">
                                                <strong>{displayDriver(violation)}</strong>
                                            </td>
                                            <td data-label="Numer karty">
                                                {violation.driverCardNumber || "Brak danych"}
                                            </td>
                                            <td data-label="Typ naruszenia">{displayViolationType(violation)}</td>
                                            <td data-label="Data">{formatDate(violation.occurredAtUtc)}</td>
                                            <td data-label="Opis" className="violation-description">
                                                {violation.description}
                                            </td>
                                            <td data-label="Skala">
                                                <span className="violation-scale-label">{getViolationScaleLabel(violation)}</span>
                                            </td>
                                            <td data-label="Poziom">
                                                <StatusBadge label={getSeverityLabel(violation.severity)} tone={severityTones[severity]} />
                                            </td>
                                            <td data-label="Status">
                                                <StatusBadge label={statusLabels[status]} tone={statusTones[status]} />
                                            </td>
                                            <td data-label="Szczegóły">
                                                <button
                                                    className="violation-details-button"
                                                    type="button"
                                                    onClick={(event) => {
                                                        event.stopPropagation();
                                                        openViolationDetails(violation);
                                                    }}
                                                >
                                                    Otwórz
                                                </button>
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                </section>
            )}

            {selectedViolation && (
                <ViolationDetailsModal
                    violation={selectedViolation}
                    onClose={closeSelectedViolation}
                />
            )}
        </div>
    );
}

type SummaryCardProps = {
    label: string;
    value: number;
    tone: "total" | SeverityLevel;
    description: string;
};

function SummaryCard({ label, value, tone, description }: SummaryCardProps) {
    return (
        <article className={`violation-summary-card ${tone}`}>
            <span>{label}</span>
            <strong>{value}</strong>
            <small>{description}</small>
        </article>
    );
}

type ComplianceAssistantEvent = {
    id: string;
    time: Date;
    icon: string;
    description: string;
    tone?: "neutral" | "warning" | "danger" | "success";
};

const assistantTimeFormatter = new Intl.DateTimeFormat("pl-PL", {
    hour: "2-digit",
    minute: "2-digit",
    timeZone: "UTC",
});

function formatAssistantTime(date: Date) {
    return assistantTimeFormatter.format(date);
}

function getActivityTypeLabel(activityType: string) {
    const normalized = activityType.trim().toUpperCase();

    if (normalized.includes("DRIVING")) return "Jazda";
    if (normalized.includes("WORK")) return "Praca";
    if (normalized.includes("AVAILABILITY")) return "Dyspozycyjność";
    if (normalized.includes("REST") || normalized.includes("BREAK")) return "Odpoczynek";

    return "Aktywność";
}

function getActivityIcon(activityType: string) {
    const normalized = activityType.trim().toUpperCase();

    if (normalized.includes("DRIVING")) return "▶";
    if (normalized.includes("WORK")) return "⚒";
    if (normalized.includes("AVAILABILITY")) return "◷";
    if (normalized.includes("REST") || normalized.includes("BREAK")) return "☕";

    return "•";
}

function isDrivingActivity(activityType: string) {
    return activityType.trim().toUpperCase().includes("DRIVING");
}

function isRestLikeActivity(activityType: string) {
    const normalized = activityType.trim().toUpperCase();

    return normalized.includes("REST") || normalized.includes("BREAK") || normalized.includes("AVAILABILITY");
}

function getActivityVehicleLabel(activity: TimelineActivity) {
    return activity.vehicleRegistration || activity.vehicleRegistrationNumber || activity.vehicle || "";
}

function getActivityDurationMinutes(activity: TimelineActivity, start: Date, end: Date) {
    const durationFromRange = Math.max(0, Math.round((end.getTime() - start.getTime()) / 60000));

    if (durationFromRange > 0) {
        return durationFromRange;
    }

    return Math.max(0, Math.round((activity.durationSeconds ?? 0) / 60));
}

function buildActivityEventDescription(activity: TimelineActivity, start: Date, end: Date) {
    const typeLabel = getActivityTypeLabel(activity.activityType);
    const durationMinutes = getActivityDurationMinutes(activity, start, end);
    const vehicleLabel = getActivityVehicleLabel(activity);
    const durationText = durationMinutes > 0 ? " " + formatMinutes(durationMinutes) : "";
    const vehicleText = vehicleLabel ? ", pojazd " + vehicleLabel : "";

    if (isDrivingActivity(activity.activityType)) {
        return typeLabel + durationText + vehicleText;
    }

    if (isRestLikeActivity(activity.activityType)) {
        return typeLabel + durationText;
    }

    return typeLabel + durationText + vehicleText;
}

function getViolationAssistantDescription(violation: DriverViolation) {
    const normalizedCode = violation.code.trim().toUpperCase();
    const violationName = displayViolationType(violation);
    const durationText = getViolationDuration(violation);

    if (normalizedCode.includes("CONTINUOUS_DRIVING")) {
        return durationText === "Brak danych"
            ? "W tym miejscu wykryto problem z przerwą po ciągłej jeździe."
            : "Wykryto problem z przerwą po ciągłej jeździe: " + durationText + ".";
    }

    if (normalizedCode.includes("DAILY_DRIVING")) {
        return durationText === "Brak danych"
            ? "W tym miejscu wykryto przekroczenie dziennego czasu jazdy."
            : "Wykryto przekroczenie dziennego czasu jazdy: " + durationText + ".";
    }

    if (normalizedCode.includes("DAILY_REST")) {
        return durationText === "Brak danych"
            ? "W tym miejscu wykryto problem z odpoczynkiem dziennym."
            : "Wykryto problem z odpoczynkiem dziennym: " + durationText + ".";
    }

    if (normalizedCode.includes("WEEKLY")) {
        return durationText === "Brak danych"
            ? "W tym miejscu wykryto problem z limitem lub odpoczynkiem tygodniowym."
            : "Wykryto problem tygodniowy: " + durationText + ".";
    }

    return violation.description || "Wykryto naruszenie: " + violationName + ".";
}

function buildComplianceAssistantEvents(
    violation: DriverViolation,
    activities: TimelineActivity[],
): ComplianceAssistantEvent[] {
    const period = getViolationPeriod(violation);

    if (!period.start) {
        return [];
    }

    const dayStart = getUtcDayStart(period.start);
    const dayEnd = addDays(dayStart, 1);
    const rangeStart = period.hasExactRange ? period.start : dayStart;
    const rangeEnd = period.end ?? dayEnd;
    const parsedActivities = activities
        .map((activity) => {
            const start = parseUtcDate(activity.startUtc);
            const end = parseUtcDate(activity.endUtc);

            if (!start || !end || end <= rangeStart || start >= rangeEnd) {
                return null;
            }

            return {
                activity,
                start: start < rangeStart ? rangeStart : start,
                end: end > rangeEnd ? rangeEnd : end,
            };
        })
        .filter((item): item is { activity: TimelineActivity; start: Date; end: Date } => item !== null)
        .sort((left, right) => left.start.getTime() - right.start.getTime());

    const events: ComplianceAssistantEvent[] = [];
    const firstDriving = parsedActivities.find((item) => isDrivingActivity(item.activity.activityType));

    if (firstDriving) {
        events.push({
            id: "driving-start-" + firstDriving.activity.id,
            time: firstDriving.start,
            icon: "▶",
            description: "Rozpoczęcie jazdy widocznej w analizowanym zakresie.",
        });
    }

    for (const item of parsedActivities) {
        events.push({
            id: "activity-" + item.activity.id + "-" + item.start.toISOString(),
            time: item.start,
            icon: getActivityIcon(item.activity.activityType),
            description: buildActivityEventDescription(item.activity, item.start, item.end),
        });
    }

    events.push({
        id: "violation-" + (violation.id || violation.occurredAtUtc),
        time: period.start,
        icon: "⚠",
        description: getViolationAssistantDescription(violation),
        tone: "danger",
    });

    const nextRest = parsedActivities.find(
        (item) => item.start > period.start! && isRestLikeActivity(item.activity.activityType),
    );

    if (nextRest) {
        events.push({
            id: "next-rest-" + nextRest.activity.id,
            time: nextRest.start,
            icon: "🛑",
            description: "Rozpoczęcie przerwy lub odpoczynku po zdarzeniu.",
            tone: "success",
        });
    }

    return events
        .sort((left, right) => left.time.getTime() - right.time.getTime())
        .filter((event, index, allEvents) => {
            const previous = allEvents[index - 1];

            return !previous
                || previous.time.getTime() !== event.time.getTime()
                || previous.description !== event.description;
        });
}



function formatActivityLabel(activityType: string) {
    const normalized = activityType.trim().toUpperCase();

    if (normalized === "DRIVING") return "Jazda";
    if (normalized === "WORK") return "Praca";
    if (normalized === "AVAILABILITY") return "Dyspozycyjność";
    if (normalized === "REST") return "Odpoczynek";

    return activityType || "Nieznane";
}

function formatRuleSegmentImpact(segment: { increasesDrivingCounter: boolean; resetsCounter: boolean }) {
    if (segment.increasesDrivingCounter) {
        return "zwiększa licznik jazdy";
    }

    if (segment.resetsCounter) {
        return "resetuje licznik";
    }

    return "bez zmiany licznika";
}

function ViolationDetailsModal({
    violation,
    onClose,
}: {
    violation: DriverViolation;
    onClose: () => void;
}) {
    const severity = normalizeSeverity(violation.severity);
    const status = normalizeStatus(violation.status);
    const businessDetails = getBusinessDetailsRows(violation);
    const timelineDays = useMemo(() => buildViolationTimelineDays(violation), [violation]);
    const timelineRange = useMemo(() => getViolationTimelineFetchRange(timelineDays), [timelineDays]);
    const violationPeriod = useMemo(() => getViolationPeriod(violation), [violation]);
    const [timelineActivities, setTimelineActivities] = useState<TimelineActivity[]>([]);
    const [isTimelineLoading, setIsTimelineLoading] = useState(false);
    const [timelineError, setTimelineError] = useState("");
    const hasBusinessDetails =
        businessDetails.summary.length > 0 || businessDetails.rows.length > 0;
    const complianceAssistantEvents = useMemo(
        () => buildComplianceAssistantEvents(violation, timelineActivities),
        [timelineActivities, violation],
    );
    const dispatcherKpis = useMemo(() => getDispatcherKpis(violation), [violation]);
    const dispatcherNextSteps = useMemo(() => getDispatcherNextSteps(violation), [violation]);
    const dispatcherScaleBadge = useMemo(() => getDispatcherScaleBadge(violation), [violation]);

    useEffect(() => {
        async function loadTimelineActivities() {
            if (!violation.driverCardNumber || !timelineRange) {
                setTimelineActivities([]);
                setIsTimelineLoading(false);
                return;
            }

            setIsTimelineLoading(true);
            setTimelineError("");

            try {
                setTimelineActivities(await getDriverActivitiesByCard(
                    violation.driverCardNumber,
                    timelineRange.from,
                    timelineRange.to,
                ));
            } catch (loadError) {
                setTimelineError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania aktywności do osi czasu.",
                );
            } finally {
                setIsTimelineLoading(false);
            }
        }

        void loadTimelineActivities();
    }, [timelineRange, violation.driverCardNumber]);

    return (
        <div className="violation-modal-backdrop" role="presentation" onClick={onClose}>
            <aside
                className="violation-details-modal"
                role="dialog"
                aria-modal="true"
                aria-labelledby="violation-details-title"
                onClick={(event) => event.stopPropagation()}
            >
                <div className="violation-details-header">
                    <div>
                        <span>Szczegóły naruszenia</span>
                        <h3 id="violation-details-title">{displayViolationType(violation)}</h3>
                    </div>
                    <button
                        type="button"
                        onClick={(event) => {
                            event.stopPropagation();
                            onClose();
                        }}
                        aria-label="Zamknij szczegóły"
                    >
                        ×
                    </button>
                </div>

                <div className="violation-details-badges">
                    <StatusBadge label={getSeverityLabel(violation.severity)} tone={severityTones[severity]} />
                    <StatusBadge label={statusLabels[status]} tone={statusTones[status]} />
                </div>

                {violation.dispatcherRecommendation && (
                    <section className={`violation-details-section dispatcher-assistant-card ${violation.dispatcherRecommendation.status.toLowerCase().replaceAll("_", "-")}`}>
                        <div className="dispatcher-assistant-statusbar">
                            <span className="dispatcher-assistant-icon" aria-hidden="true">
                                {getDispatcherStatusIcon(violation.dispatcherRecommendation.status)}
                            </span>
                            <div>
                                <span>Asystent Dyspozytora</span>
                                <h4>{getDispatcherStatusLabel(violation.dispatcherRecommendation.status)}</h4>
                            </div>
                            <div className="dispatcher-assistant-statusmeta">
                                {dispatcherScaleBadge && (
                                    <span className={`dispatcher-scale-badge ${dispatcherScaleBadge.tone}`}>
                                        Skala: {dispatcherScaleBadge.label}
                                    </span>
                                )}
                                <strong>{violation.dispatcherRecommendation.plannerAttentionRequired ? "Wymaga uwagi" : "Informacyjnie"}</strong>
                            </div>
                        </div>
                        <p>{violation.dispatcherRecommendation.summary}</p>
                        {getDispatcherReason(violation) && (
                            <div className="dispatcher-assistant-reason">
                                <span>Powód</span>
                                <strong>{getDispatcherReason(violation)}</strong>
                            </div>
                        )}
                        {dispatcherKpis.length > 0 && (
                            <div className="dispatcher-assistant-kpis" aria-label="Wartości biznesowe">
                                {dispatcherKpis.map((kpi) => (
                                    <div className={`dispatcher-assistant-kpi ${kpi.tone}`} key={kpi.label}>
                                        <span>{kpi.label}</span>
                                        <strong>{kpi.value}</strong>
                                    </div>
                                ))}
                            </div>
                        )}
                        {!isTimelineLoading && !timelineError && (
                            <ViolationTimelinePreview
                                activities={timelineActivities}
                                occurredAtUtc={violation.occurredAtUtc}
                                periodEndUtc={violation.periodEndUtc}
                            />
                        )}
                        <div className="dispatcher-assistant-flags" aria-label="Ocena operacyjna">
                            <span>{formatDispatcherFlag(violation.dispatcherRecommendation.canDrive, "✓ Można jechać", "🚫 Nie planuj jazdy")}</span>
                            <span>{formatDispatcherFlag(violation.dispatcherRecommendation.canStartShift, "✓ Można rozpocząć zmianę", "🚫 Nie zaczynaj zmiany")}</span>
                            <span>{formatDispatcherFlag(violation.dispatcherRecommendation.plannerAttentionRequired, "👤 Uwaga planisty", "✓ Bez pilnej reakcji")}</span>
                            {violation.dispatcherRecommendation.earliestNextDriveUtc && (
                                <span>Najwcześniej: {formatDate(violation.dispatcherRecommendation.earliestNextDriveUtc)}</span>
                            )}
                        </div>
                        {(violation.dispatcherRecommendation.recommendedActions.length > 0 || dispatcherNextSteps.length > 0) && (
                            <div className="dispatcher-assistant-actions-block">
                                {violation.dispatcherRecommendation.recommendedActions.length > 0 && (
                                    <>
                                        <div className="dispatcher-assistant-primary-action">
                                            <span>Najważniejsze działanie</span>
                                            <strong>{violation.dispatcherRecommendation.recommendedActions[0]}</strong>
                                        </div>
                                        {violation.dispatcherRecommendation.recommendedActions.length > 1 && (
                                            <div className="dispatcher-assistant-secondary-actions">
                                                <span>Dodatkowo</span>
                                                <ul className="dispatcher-assistant-actions">
                                                    {violation.dispatcherRecommendation.recommendedActions.slice(1).map((action) => (
                                                        <li key={action}>{action}</li>
                                                    ))}
                                                </ul>
                                            </div>
                                        )}
                                    </>
                                )}
                                {dispatcherNextSteps.length > 0 && (
                                    <div className="dispatcher-assistant-next-steps">
                                        <span>Następne kroki</span>
                                        <ol>
                                            {dispatcherNextSteps.map((step) => (
                                                <li key={step}>{step}</li>
                                            ))}
                                        </ol>
                                    </div>
                                )}
                            </div>
                        )}
                    </section>
                )}

                <dl className="violation-details-grid">
                    <div><dt>Kierowca</dt><dd>{displayDriver(violation)}</dd></div>
                    <div><dt>Numer karty</dt><dd>{violation.driverCardNumber || "Brak danych"}</dd></div>
                    <div><dt>Start</dt><dd>{formatDate(violation.occurredAtUtc)}</dd></div>
                    <div><dt>Koniec</dt><dd>{violation.periodEndUtc ? formatDate(violation.periodEndUtc) : "Brak dokładnego zakresu"}</dd></div>
                    <div><dt>Czas / przekroczenie</dt><dd>{getViolationDuration(violation)}</dd></div>
                </dl>

                {hasBusinessDetails && (
                    <section className="violation-details-section">
                        <h4>Szczegóły</h4>
                        {businessDetails.summary && <p>{businessDetails.summary}</p>}
                        {businessDetails.rows.length > 0 && (
                            <dl className="violation-business-details">
                                {businessDetails.rows.map(([label, value]) => (
                                    <div key={label}>
                                        <dt>{label}</dt>
                                        <dd>{value}</dd>
                                    </div>
                                ))}
                            </dl>
                        )}
                    </section>
                )}



                {violation.ruleAnalysis && (
                    <section className="violation-details-section rule-analysis-card">
                        <div className="rule-analysis-heading">
                            <div>
                                <span>Analiza reguły</span>
                                <h4>{violation.ruleAnalysis.ruleName}</h4>
                            </div>
                            <strong>{violation.ruleAnalysis.isEstimated ? "Odtworzona z aktywności" : "Z metadanych silnika"}</strong>
                        </div>
                        <p>{violation.ruleAnalysis.businessSummary}</p>
                        <dl className="rule-analysis-metrics">
                            <div><dt>Kod reguły</dt><dd>{violation.ruleAnalysis.ruleCode}</dd></div>
                            <div><dt>Limit jazdy</dt><dd>{formatMinutes(violation.ruleAnalysis.drivingLimitMinutes)}</dd></div>
                            <div><dt>Wymagana przerwa</dt><dd>{formatMinutes(violation.ruleAnalysis.requiredBreakMinutes)}</dd></div>
                            <div><dt>Moment wykrycia</dt><dd>{formatDate(violation.ruleAnalysis.violationDetectedAtUtc)}</dd></div>
                            <div><dt>Jazda bez poprawnej przerwy</dt><dd>{formatMinutes(violation.ruleAnalysis.continuousDrivingMinutes)}</dd></div>
                            <div><dt>Przekroczenie</dt><dd>{formatMinutes(violation.ruleAnalysis.exceededMinutes)}</dd></div>
                        </dl>
                        {violation.ruleAnalysis.segments.length > 0 && (
                            <div className="rule-analysis-table-wrapper">
                                <table className="rule-analysis-table">
                                    <thead>
                                        <tr>
                                            <th>Od</th>
                                            <th>Do</th>
                                            <th>Typ</th>
                                            <th>Czas</th>
                                            <th>Wpływ na licznik</th>
                                            <th>Licznik po segmencie</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {violation.ruleAnalysis.segments.map((segment) => (
                                            <tr key={segment.startUtc + segment.endUtc + segment.activityType}>
                                                <td>{formatDate(segment.startUtc)}</td>
                                                <td>{formatDate(segment.endUtc)}</td>
                                                <td>{formatActivityLabel(segment.activityType)}</td>
                                                <td>{formatMinutes(segment.durationMinutes)}</td>
                                                <td>{formatRuleSegmentImpact(segment)}</td>
                                                <td>{formatMinutes(segment.drivingCounterAfterSegment)}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </section>
                )}


                <section className="violation-details-section">
                    <h4>Opis</h4>
                    <p>{violation.description || "Brak opisu naruszenia."}</p>
                </section>

                <section className="violation-details-section violation-timeline-section">
                    <h4>Kontekst na osi czasu</h4>
                    {!violationPeriod.hasExactRange && (
                        <p className="violation-timeline-note">Brak dokładnego zakresu naruszenia. Pokazano dzień wystąpienia zdarzenia.</p>
                    )}
                    {!violation.driverCardNumber && (
                        <p className="violation-timeline-note">Brak numeru karty kierowcy, więc nie można pobrać aktywności do wykresu.</p>
                    )}
                    {isTimelineLoading ? (
                        <div className="violation-timeline-loading" aria-busy="true">
                            Ładowanie osi czasu...
                        </div>
                    ) : timelineError ? (
                        <p className="violation-timeline-error" role="alert">{timelineError}</p>
                    ) : timelineDays.length === 0 ? (
                        <p className="violation-timeline-note">Brak daty, dla której można zbudować oś czasu.</p>
                    ) : (
                        <div className="violation-timeline-days">
                            {timelineDays.map((day) => (
                                <TachographTimeline
                                    activities={timelineActivities}
                                    day={day.date}
                                    key={day.date}
                                    label={day.label}
                                    violations={violationPeriod.hasExactRange ? [violation] : []}
                                />
                            ))}
                        </div>
                    )}
                </section>

                <section className="violation-details-section violation-assistant-section">
                    <h4>Analiza przebiegu zdarzenia</h4>
                    {isTimelineLoading ? (
                        <p className="violation-timeline-note">Analiza zostanie pokazana po załadowaniu aktywności.</p>
                    ) : complianceAssistantEvents.length === 0 ? (
                        <p className="violation-timeline-note">Brak wystarczających danych aktywności, aby odtworzyć przebieg zdarzenia.</p>
                    ) : (
                        <ol className="violation-assistant-events">
                            {complianceAssistantEvents.map((event) => (
                                <li className={event.tone} key={event.id}>
                                    <time>{formatAssistantTime(event.time)}</time>
                                    <span aria-hidden="true">{event.icon}</span>
                                    <p>{event.description}</p>
                                </li>
                            ))}
                        </ol>
                    )}
                </section>

                <section className="violation-details-section">
                    <h4>Wyjaśnienie prawne i biznesowe</h4>
                    <p>{getLegalExplanation(violation)}</p>
                </section>
                <section className="violation-details-section recommendation">
                    <h4>Rekomendowana akcja</h4>
                    <p>{violation.recommendation || getRecommendedAction(violation)}</p>
                </section>
            </aside>
        </div>
    );
}