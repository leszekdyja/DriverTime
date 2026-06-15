import { useEffect, useState } from "react";

import {
    getDriverViolations,
    type DriverViolation,
} from "../services/violationsService";
import { exportViolationsPdf } from "../services/pdfExportService";
import "../styles/violations.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

const severityLabels: Record<DriverViolation["severity"], string> = {
    low: "Niski",
    medium: "Sredni",
    high: "Wysoki",
};

function formatDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function displayDriver(violation: DriverViolation) {
    return (
        [violation.driverFirstName, violation.driverLastName]
            .filter(Boolean)
            .join(" ") || "Brak danych"
    );
}

export default function ViolationsPage() {
    const [violations, setViolations] = useState<DriverViolation[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isGeneratingPdf, setIsGeneratingPdf] = useState(false);
    const [error, setError] = useState("");

    async function handlePdfExport() {
        setIsGeneratingPdf(true);
        setError("");

        try {
            await exportViolationsPdf(violations);
        } catch {
            setError("Nie udalo sie wygenerowac pliku PDF.");
        } finally {
            setIsGeneratingPdf(false);
        }
    }

    useEffect(() => {
        async function loadViolations() {
            try {
                setViolations(await getDriverViolations());
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystapil blad podczas pobierania naruszen.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadViolations();
    }, []);

    return (
        <div className="violations-page">
            <div className="violations-heading">
                <div>
                    <h2>Naruszenia</h2>
                    <p>Podstawowa analiza czasu jazdy na podstawie danych DDD.</p>
                </div>
                {!isLoading && !error && (
                    <div className="violations-heading-actions">
                        <span className="violations-count">
                            {violations.length} naruszen
                        </span>
                        <button
                            className="violations-pdf-button"
                            type="button"
                            onClick={() => void handlePdfExport()}
                            disabled={violations.length === 0 || isGeneratingPdf}
                        >
                            {isGeneratingPdf ? "Generowanie PDF..." : "Eksport PDF"}
                        </button>
                    </div>
                )}
            </div>

            {isLoading && (
                <section className="violations-panel" aria-busy="true">
                    <div className="violations-skeleton heading" />
                    <div className="violations-skeleton row" />
                    <div className="violations-skeleton row" />
                    <div className="violations-skeleton row" />
                </section>
            )}

            {error && (
                <p className="violations-error" role="alert">
                    {error}
                </p>
            )}

            {!isLoading && !error && violations.length === 0 && (
                <section className="violations-empty">
                    <strong>Brak wykrytych naruszen</strong>
                    <p>
                        Zaimportowane aktywnosci nie przekraczaja obecnie
                        obslugiwanych limitow.
                    </p>
                </section>
            )}

            {!isLoading && !error && violations.length > 0 && (
                <section className="violations-panel">
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
                                </tr>
                            </thead>
                            <tbody>
                                {violations.map((violation, index) => (
                                    <tr
                                        key={`${violation.driverCardNumber}-${violation.occurredAtUtc}-${violation.violationType}-${index}`}
                                    >
                                        <td>{displayDriver(violation)}</td>
                                        <td>
                                            {violation.driverCardNumber || "Brak danych"}
                                        </td>
                                        <td>{violation.violationType}</td>
                                        <td>{formatDate(violation.occurredAtUtc)}</td>
                                        <td className="violation-description">
                                            {violation.description}
                                        </td>
                                        <td>
                                            <span
                                                className={`severity-badge ${violation.severity}`}
                                            >
                                                {severityLabels[violation.severity]}
                                            </span>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </section>
            )}
        </div>
    );
}
