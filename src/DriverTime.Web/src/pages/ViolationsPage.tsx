import { useEffect, useMemo, useState, type FormEvent } from "react";

import { EmptyState, TableSkeleton } from "../components/UiStates";
import { exportViolationsExcel } from "../services/excelExportService";
import { exportViolationsPdf } from "../services/pdfExportService";
import {
    getDriverViolations,
    type DriverViolation,
} from "../services/violationsService";
import "../styles/violations.css";

type SeverityLevel = "minor" | "serious" | "very-serious";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const severityLabels: Record<SeverityLevel, string> = {
    minor: "Minor",
    serious: "Serious",
    "very-serious": "Very serious",
};

const severityDescriptions: Record<SeverityLevel, string> = {
    minor: "Naruszenia o niższym priorytecie",
    serious: "Naruszenia wymagające weryfikacji",
    "very-serious": "Naruszenia krytyczne dla zgodności",
};

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
    return (
        [violation.driverFirstName, violation.driverLastName]
            .filter(Boolean)
            .join(" ") || "Brak danych"
    );
}

function normalizeSeverity(severity: string): SeverityLevel {
    const value = severity.trim().toLowerCase().replaceAll("_", " ");

    if (value === "high" || value === "severe" || value === "very serious") {
        return "very-serious";
    }

    if (value === "medium" || value === "serious") {
        return "serious";
    }

    return "minor";
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

export default function ViolationsPage() {
    const [violations, setViolations] = useState<DriverViolation[]>([]);
    const [selectedDriver, setSelectedDriver] = useState("");
    const [selectedType, setSelectedType] = useState("");
    const [dateFrom, setDateFrom] = useState("");
    const [dateTo, setDateTo] = useState("");
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [isGeneratingExcel, setIsGeneratingExcel] = useState(false);
    const [error, setError] = useState("");
    const [filterError, setFilterError] = useState("");

    useEffect(() => {
        async function loadViolations() {
            setIsLoading(true);
            setError("");

            try {
                setViolations(await getDriverViolations());
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

    const drivers = useMemo(() => {
        const result = new Map<string, string>();

        for (const violation of violations) {
            const cardNumber = violation.driverCardNumber || "Brak numeru karty";
            const label = `${displayDriver(violation)} (${cardNumber})`;
            result.set(cardNumber, label);
        }

        return [...result.entries()].sort((left, right) =>
            left[1].localeCompare(right[1], "pl"),
        );
    }, [violations]);

    const violationTypes = useMemo(() => (
        [...new Set(violations.map((violation) => violation.violationType).filter(Boolean))]
            .sort((left, right) => left.localeCompare(right, "pl"))
    ), [violations]);

    const filteredViolations = useMemo(() => {
        if (filterError) return [];

        return violations.filter((violation) => {
            const occurredDate = formatDateForInput(violation.occurredAtUtc);

            if (selectedDriver && (violation.driverCardNumber || "Brak numeru karty") !== selectedDriver) {
                return false;
            }

            if (selectedType && violation.violationType !== selectedType) {
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
    }, [dateFrom, dateTo, filterError, selectedDriver, selectedType, violations]);

    const summary = useMemo(() => {
        const result = {
            total: filteredViolations.length,
            minor: 0,
            serious: 0,
            verySerious: 0,
        };

        for (const violation of filteredViolations) {
            const severity = normalizeSeverity(violation.severity);

            if (severity === "very-serious") {
                result.verySerious++;
            } else if (severity === "serious") {
                result.serious++;
            } else {
                result.minor++;
            }
        }

        return result;
    }, [filteredViolations]);

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
    }

    function clearFilters() {
        setSelectedDriver("");
        setSelectedType("");
        setDateFrom("");
        setDateTo("");
        setFilterError("");
    }

    return (
        <div className="violations-page">
            <section className="violations-hero">
                <div>
                    <span className="violations-eyebrow">Compliance</span>
                    <h2>Naruszenia czasu pracy kierowców</h2>
                    <p>
                        Monitoruj naruszenia wykryte na podstawie aktywności DDD,
                        filtruj je według kierowcy, typu i zakresu dat oraz eksportuj wyniki.
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
                <SummaryCard label="Wszystkie" value={summary.total} tone="total" description="Wynik po filtrach" />
                <SummaryCard label={severityLabels.minor} value={summary.minor} tone="minor" description={severityDescriptions.minor} />
                <SummaryCard label={severityLabels.serious} value={summary.serious} tone="serious" description={severityDescriptions.serious} />
                <SummaryCard label={severityLabels["very-serious"]} value={summary.verySerious} tone="very-serious" description={severityDescriptions["very-serious"]} />
            </section>

            <form className="violations-filters" onSubmit={handleFilterSubmit}>
                <label>
                    Kierowca
                    <select value={selectedDriver} onChange={(event) => setSelectedDriver(event.target.value)}>
                        <option value="">Wszyscy kierowcy</option>
                        {drivers.map(([cardNumber, label]) => (
                            <option key={cardNumber} value={cardNumber}>
                                {label}
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Typ naruszenia
                    <select value={selectedType} onChange={(event) => setSelectedType(event.target.value)}>
                        <option value="">Wszystkie typy</option>
                        {violationTypes.map((type) => (
                            <option key={type} value={type}>
                                {type}
                            </option>
                        ))}
                    </select>
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
                        description="Zmień kierowcę, typ naruszenia albo zakres dat. Po imporcie nowych plików DDD lista odświeży się na podstawie danych backendowych."
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
                                    <th>Czas / przekroczenie</th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredViolations.map((violation, index) => {
                                    const severity = normalizeSeverity(violation.severity);

                                    return (
                                        <tr
                                            key={`${violation.driverCardNumber}-${violation.occurredAtUtc}-${violation.violationType}-${index}`}
                                        >
                                            <td data-label="Kierowca">
                                                <strong>{displayDriver(violation)}</strong>
                                            </td>
                                            <td data-label="Numer karty">
                                                {violation.driverCardNumber || "Brak danych"}
                                            </td>
                                            <td data-label="Typ naruszenia">{violation.violationType}</td>
                                            <td data-label="Data">{formatDate(violation.occurredAtUtc)}</td>
                                            <td data-label="Opis" className="violation-description">
                                                {violation.description}
                                            </td>
                                            <td data-label="Poziom">
                                                <span className={`severity-badge ${severity}`}>
                                                    {severityLabels[severity]}
                                                </span>
                                            </td>
                                            <td data-label="Czas / przekroczenie">
                                                {getViolationDuration(violation)}
                                            </td>
                                        </tr>
                                    );
                                })}
                            </tbody>
                        </table>
                    </div>
                </section>
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
