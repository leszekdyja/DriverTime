import { useEffect, useMemo, useState, type FormEvent, type KeyboardEvent } from "react";
import { useSearchParams } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getDrivers,
    type ComplianceDriver,
} from "../services/complianceService";
import { exportViolationsExcel } from "../services/excelExportService";
import { exportViolationsPdf } from "../services/pdfExportService";
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

const readAlertsStorageKey = "drivertime.violationAlerts.read";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const severityLabels: Record<SeverityLevel, string> = {
    info: "Info",
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
    info: "Info",
    warning: "Ostrzeżenie",
    serious: "Ostrzeżenie",
    critical: "Krytyczne",
};

const alertSeverityTones: Record<AlertSeverity, "info" | "warning" | "danger" | "critical"> = {
    info: "info",
    warning: "warning",
    serious: "danger",
    critical: "critical",
};

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

    return `${hours} godz. ${restMinutes.toString().padStart(2, "0")} min`;
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
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
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
        if (!violationIdFromQuery || selectedViolation) {
            return;
        }

        const matchingViolation = violations.find(
            (violation) => violation.id === violationIdFromQuery,
        );

        if (matchingViolation) {
            setSelectedViolation(matchingViolation);
        }
    }, [selectedViolation, violationIdFromQuery, violations]);

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
            setSelectedViolation(violation);
        }
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
                    <span className="violations-eyebrow">Compliance</span>
                    <h2>Naruszenia czasu pracy kierowców</h2>
                    <p>
                        Monitoruj naruszenia wykryte na podstawie aktywności DDD,
                        filtruj je według kierowcy, wagi, statusu i zakresu dat.
                    </p>
                </div>
                {!isLoading && !error && (
                    <div className="violations-heading-actions">
                        <button
                            className="violations-pdf-button"
                            type="button"
                            onClick={() => void handlePdfExport()}
                            disabled={filteredViolations.length === 0 || isGeneratingPdf || isGeneratingExcel}
                        >
                            {isGeneratingPdf ? "Generowanie PDF..." : "Eksport PDF"}
                        </button>
                        <button
                            className="violations-export-button"
                            type="button"
                            onClick={() => void handleExcelExport()}
                            disabled={filteredViolations.length === 0 || isGeneratingPdf || isGeneratingExcel}
                        >
                            {isGeneratingExcel ? "Generowanie Excel..." : "Eksport Excel"}
                        </button>
                    </div>
                )}
            </section>

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
                        <option value="info">Info</option>
                        <option value="warning">Warning</option>
                        <option value="critical">Critical</option>
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
                                            onClick={() => setSelectedViolation(violation)}
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
                                                        setSelectedViolation(violation);
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
                    onClose={() => setSelectedViolation(null)}
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

function ViolationDetailsModal({
    violation,
    onClose,
}: {
    violation: DriverViolation;
    onClose: () => void;
}) {
    const severity = normalizeSeverity(violation.severity);
    const status = normalizeStatus(violation.status);

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
                    <button type="button" onClick={onClose} aria-label="Zamknij szczegóły">
                        ×
                    </button>
                </div>

                <div className="violation-details-badges">
                    <StatusBadge label={getSeverityLabel(violation.severity)} tone={severityTones[severity]} />
                    <StatusBadge label={statusLabels[status]} tone={statusTones[status]} />
                </div>

                <dl className="violation-details-grid">
                    <div><dt>Kierowca</dt><dd>{displayDriver(violation)}</dd></div>
                    <div><dt>Numer karty</dt><dd>{violation.driverCardNumber || "Brak danych"}</dd></div>
                    <div><dt>Start</dt><dd>{formatDate(violation.occurredAtUtc)}</dd></div>
                    <div><dt>Koniec</dt><dd>{formatDate(violation.periodEndUtc || violation.occurredAtUtc)}</dd></div>
                    <div><dt>Czas / przekroczenie</dt><dd>{getViolationDuration(violation)}</dd></div>
                    <div><dt>Kod</dt><dd>{violation.code || "Brak kodu"}</dd></div>
                </dl>

                <section className="violation-details-section">
                    <h4>Opis</h4>
                    <p>{violation.description || "Brak opisu naruszenia."}</p>
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
