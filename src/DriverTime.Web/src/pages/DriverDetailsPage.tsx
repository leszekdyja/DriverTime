import { memo, useEffect, useState, type ReactNode } from "react";
import { Link, useParams } from "react-router-dom";

import DriverActivityCalendar from "../components/DriverActivityCalendar";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    getComplianceViolationsForDriver,
    type ComplianceDriver,
} from "../services/complianceService";
import {
    getDriverDetails,
    type DriverDetails,
} from "../services/driverDetailsService";
import type { DriverViolation } from "../services/violationsService";
import "../styles/driver-details.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function formatDate(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatDuration(seconds: number) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours} godz. ${minutes} min`;
}

function formatMinutes(minutes: number) {
    return formatDuration(minutes * 60);
}

function formatViolationDuration(violation: DriverViolation) {
    if (violation.actualDurationMinutes <= 0) {
        return "Brak danych";
    }

    const actual = formatMinutes(violation.actualDurationMinutes);
    const exceededBy = Math.max(
        violation.actualDurationMinutes - violation.limitDurationMinutes,
        0,
    );

    return exceededBy > 0
        ? `${actual} (+${formatMinutes(exceededBy)})`
        : actual;
}

function getSeverityClass(severity: string) {
    const normalized = severity.toLowerCase();

    if (normalized === "critical" || normalized === "high" || normalized === "severe") return "high";
    if (normalized === "warning" || normalized === "medium") return "medium";
    if (normalized === "info" || normalized === "low") return "low";
    return "default";
}

export default function DriverDetailsPage() {
    const { id } = useParams<{ id: string }>();
    const [details, setDetails] = useState<DriverDetails | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");
    const [violations, setViolations] = useState<DriverViolation[]>([]);
    const [areViolationsLoading, setAreViolationsLoading] = useState(true);
    const [violationsError, setViolationsError] = useState("");

    useEffect(() => {
        async function loadDetails() {
            if (!id) {
                setError("Brak identyfikatora kierowcy.");
                setIsLoading(false);
                return;
            }

            try {
                setDetails(await getDriverDetails(id));
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania kierowcy.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadDetails();
    }, [id]);

    useEffect(() => {
        async function loadViolations() {
            if (!id || !details) {
                setAreViolationsLoading(false);
                return;
            }

            const driver: ComplianceDriver = {
                id: details.id,
                firstName: details.firstName,
                lastName: details.lastName,
                cardNumber: details.cardNumber,
            };

            setAreViolationsLoading(true);
            setViolationsError("");

            try {
                setViolations(await getComplianceViolationsForDriver(driver));
            } catch (loadError) {
                setViolationsError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania naruszeń.",
                );
            } finally {
                setAreViolationsLoading(false);
            }
        }

        void loadViolations();
    }, [details, id]);

    return (
        <div className="driver-details-page">
            <Link className="driver-back-link" to="/drivers">
                Powrót do kierowców
            </Link>

            {isLoading && <DriverDetailsSkeleton />}
            {error && <p className="driver-details-error" role="alert">{error}</p>}

            {details && (
                <>
                    <header className="driver-profile-card">
                        <div>
                            <span className="driver-profile-label">Kierowca</span>
                            <h2>{details.firstName} {details.lastName}</h2>
                            <p>{details.cardNumber || "Brak numeru karty"}</p>
                        </div>
                        <dl className="driver-profile-data">
                            <Info label="Ważność karty" value={formatDate(details.cardExpiryDate)} />
                            <Info label="Kraj wydania" value={details.cardIssuingCountry || "Brak danych"} />
                            <Info label="Utworzono" value={formatDate(details.createdAtUtc)} />
                            <Info label="Liczba importów" value={String(details.importsCount)} />
                            <Info label="Ostatni import" value={formatDate(details.lastImportAtUtc)} />
                        </dl>
                    </header>

                    <section className="driver-time-grid" aria-label="Podsumowanie czasów">
                        <TimeCard label="Jazda" seconds={details.drivingSeconds} />
                        <TimeCard label="Praca" seconds={details.workSeconds} />
                        <TimeCard label="Odpoczynek" seconds={details.restSeconds} />
                        <TimeCard label="Dyspozycyjność" seconds={details.availabilitySeconds} />
                    </section>

                    <DriverActivityCalendar driverId={details.id} />

                    <DetailsSection title="Historia importów" empty={details.recentImports.length === 0}>
                        <table><thead><tr><th>Plik</th><th>Data</th><th>Aktywności</th><th></th></tr></thead>
                            <tbody>{details.recentImports.map((item) => <tr key={item.id}><td>{item.fileName}</td><td>{formatDate(item.uploadedAtUtc)}</td><td>{item.activitiesCount}</td><td><Link className="table-link" to={`/imports/${item.id}`}>Szczegóły</Link></td></tr>)}</tbody>
                        </table>
                    </DetailsSection>

                    <DetailsSection title="Ostatnie aktywności" empty={details.recentActivities.length === 0}>
                        <table><thead><tr><th>Typ</th><th>Początek</th><th>Koniec</th><th>Czas</th></tr></thead>
                            <tbody>{details.recentActivities.map((item) => <tr key={item.id}><td>{item.activityType}</td><td>{formatDate(item.startUtc)}</td><td>{formatDate(item.endUtc)}</td><td>{formatDuration(item.durationSeconds)}</td></tr>)}</tbody>
                        </table>
                    </DetailsSection>

                    <section className="driver-details-section">
                        <h3>Naruszenia</h3>

                        {areViolationsLoading ? (
                            <TableSkeleton rows={4} columns={5} />
                        ) : violationsError ? (
                            <p className="driver-violations-error" role="alert">
                                {violationsError}
                            </p>
                        ) : violations.length === 0 ? (
                            <EmptyState
                                title="Brak naruszeń"
                                description="Engine compliance nie wykrył naruszeń dla aktywności tego kierowcy."
                            />
                        ) : (
                            <div className="driver-details-table violations-table">
                                <table>
                                    <thead><tr><th>Data i czas</th><th>Typ</th><th>Opis</th><th>Poziom</th><th>Czas / przekroczenie</th></tr></thead>
                                    <tbody>{violations.map((item, index) => <tr key={`${item.code}-${item.occurredAtUtc}-${index}`}><td>{formatDate(item.occurredAtUtc)}</td><td>{item.violationType}</td><td>{item.description}</td><td><span className={`severity-badge ${getSeverityClass(item.severity)}`}>{item.severity}</span></td><td>{formatViolationDuration(item)}</td></tr>)}</tbody>
                                </table>
                            </div>
                        )}
                    </section>

                    <DetailsSection title="Użyte pojazdy" empty={details.vehicles.length === 0}>
                        <table><thead><tr><th>Numer rejestracyjny</th><th>Pierwsze użycie</th><th>Ostatnie użycie</th><th>Liczba użyć</th></tr></thead>
                            <tbody>{details.vehicles.map((item) => <tr key={item.registrationNumber}><td>{item.registrationNumber}</td><td>{formatDate(item.firstUsedAtUtc)}</td><td>{formatDate(item.lastUsedAtUtc)}</td><td>{item.usageCount}</td></tr>)}</tbody>
                        </table>
                    </DetailsSection>
                </>
            )}
        </div>
    );
}

const Info = memo(function Info({ label, value }: { label: string; value: string }) {
    return <div><dt>{label}</dt><dd>{value}</dd></div>;
});

const TimeCard = memo(function TimeCard({ label, seconds }: { label: string; seconds: number }) {
    return <article><span>{label}</span><strong>{formatDuration(seconds)}</strong></article>;
});

function DetailsSection({ title, empty, children }: { title: string; empty: boolean; children: ReactNode }) {
    return <section className="driver-details-section"><h3>{title}</h3>{empty ? <EmptyState title={`Brak: ${title.toLocaleLowerCase("pl-PL")}`} description="Dane pojawią się po kolejnym imporcie pliku DDD." /> : <div className="driver-details-table">{children}</div>}</section>;
}

function DriverDetailsSkeleton() {
    return (
        <div className="driver-details-skeleton" aria-busy="true" aria-label="Ładowanie kierowcy">
            <div className="ui-skeleton driver-profile-skeleton" />
            <div className="driver-time-grid">
                {Array.from({ length: 4 }, (_, index) => <div className="ui-skeleton driver-time-skeleton" key={index} />)}
            </div>
            <div className="ui-skeleton driver-section-skeleton" />
            <div className="ui-skeleton driver-section-skeleton" />
        </div>
    );
}
