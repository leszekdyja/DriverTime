import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";

import {
    getDddImportDetails,
    type DddImportDetails,
} from "../services/dddImportDetailsService";
import "../styles/import-details.css";

const dateFormatter = new Intl.DateTimeFormat("pl-PL", {
    dateStyle: "medium",
    timeStyle: "short",
});

function displayValue(value: string) {
    return value || "Brak danych";
}

function formatDate(value: string) {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
        ? "Brak danych"
        : dateFormatter.format(date);
}

function getDurationSeconds(startValue: string, endValue: string) {
    const start = new Date(startValue);
    const end = new Date(endValue);

    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
        return 0;
    }

    return Math.max(Math.floor((end.getTime() - start.getTime()) / 1000), 0);
}

function formatDuration(seconds: number) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);

    return `${hours} godz. ${minutes} min`;
}

export default function ImportDetailsPage() {
    const { id } = useParams<{ id: string }>();
    const [details, setDetails] = useState<DddImportDetails | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    const activitySummary = useMemo(() => {
        const summary = {
            driving: 0,
            work: 0,
            rest: 0,
            availability: 0,
        };

        for (const activity of details?.driverActivities ?? []) {
            const duration = getDurationSeconds(activity.start, activity.end);

            switch (activity.activity.toUpperCase()) {
                case "DRIVING":
                    summary.driving += duration;
                    break;
                case "WORK":
                    summary.work += duration;
                    break;
                case "REST":
                    summary.rest += duration;
                    break;
                case "AVAILABILITY":
                    summary.availability += duration;
                    break;
            }
        }

        return summary;
    }, [details]);

    useEffect(() => {
        async function loadDetails() {
            if (!id) {
                setError("Brak identyfikatora importu DDD.");
                setIsLoading(false);
                return;
            }

            try {
                setDetails(await getDddImportDetails(id));
            } catch (loadError) {
                setError(
                    loadError instanceof Error
                        ? loadError.message
                        : "Wystąpił błąd podczas pobierania szczegółów importu.",
                );
            } finally {
                setIsLoading(false);
            }
        }

        void loadDetails();
    }, [id]);

    return (
        <div className="import-details-page">
            <Link className="back-link" to="/imports">
                Powrót do importów
            </Link>

            <h2>Szczegóły importu DDD</h2>

            {isLoading && (
                <p className="status-message" role="status">
                    Ładowanie szczegółów importu...
                </p>
            )}

            {error && (
                <p className="message error-message" role="alert">
                    {error}
                </p>
            )}

            {details && (
                <>
                    <section className="details-card">
                        <h3>Informacje o kierowcy</h3>
                        <dl className="details-summary">
                            <div>
                                <dt>Plik</dt>
                                <dd>{displayValue(details.fileName)}</dd>
                            </div>
                            <div>
                                <dt>Imie</dt>
                                <dd>{displayValue(details.driverFirstName)}</dd>
                            </div>
                            <div>
                                <dt>Nazwisko</dt>
                                <dd>{displayValue(details.driverLastName)}</dd>
                            </div>
                            <div>
                                <dt>Numer karty</dt>
                                <dd>{displayValue(details.driverCardNumber)}</dd>
                            </div>
                            <div>
                                <dt>Data importu</dt>
                                <dd>{formatDate(details.uploadedAtUtc)}</dd>
                            </div>
                        </dl>
                    </section>

                    <section className="activity-summary" aria-label="Podsumowanie aktywności">
                        <ActivitySummaryCard
                            label="Suma jazdy"
                            seconds={activitySummary.driving}
                        />
                        <ActivitySummaryCard
                            label="Suma pracy"
                            seconds={activitySummary.work}
                        />
                        <ActivitySummaryCard
                            label="Suma odpoczynku"
                            seconds={activitySummary.rest}
                        />
                        <ActivitySummaryCard
                            label="Suma dyspozycyjnosci"
                            seconds={activitySummary.availability}
                        />
                    </section>

                    <DetailsTable
                        title="Aktywności kierowcy"
                        isEmpty={details.driverActivities.length === 0}
                        headers={["Początek", "Koniec", "Aktywność", "Czas"]}
                    >
                        {details.driverActivities.map((activity, index) => (
                            <tr key={`${activity.start}-${index}`}>
                                <td>{formatDate(activity.start)}</td>
                                <td>{formatDate(activity.end)}</td>
                                <td>{displayValue(activity.activity)}</td>
                                <td>
                                    {formatDuration(
                                        getDurationSeconds(
                                            activity.start,
                                            activity.end,
                                        ),
                                    )}
                                </td>
                            </tr>
                        ))}
                    </DetailsTable>

                    <DetailsTable
                        title="Wpisy krajów"
                        isEmpty={details.countryEntries.length === 0}
                        headers={["Data", "Kod kraju", "Kraj"]}
                    >
                        {details.countryEntries.map((entry, index) => (
                            <tr key={`${entry.timestamp}-${index}`}>
                                <td>{formatDate(entry.timestamp)}</td>
                                <td>{displayValue(entry.country_code)}</td>
                                <td>{displayValue(entry.country_name)}</td>
                            </tr>
                        ))}
                    </DetailsTable>

                    <DetailsTable
                        title="Użycie pojazdów"
                        isEmpty={details.vehicleUses.length === 0}
                        headers={["Pojazd", "Początek", "Koniec"]}
                    >
                        {details.vehicleUses.map((vehicleUse, index) => (
                            <tr key={`${vehicleUse.start}-${index}`}>
                                <td>
                                    {displayValue(vehicleUse.vehicle_registration)}
                                </td>
                                <td>{formatDate(vehicleUse.start)}</td>
                                <td>{formatDate(vehicleUse.end)}</td>
                            </tr>
                        ))}
                    </DetailsTable>
                </>
            )}
        </div>
    );
}

type ActivitySummaryCardProps = {
    label: string;
    seconds: number;
};

function ActivitySummaryCard({ label, seconds }: ActivitySummaryCardProps) {
    return (
        <article className="activity-summary-card">
            <span>{label}</span>
            <strong>{formatDuration(seconds)}</strong>
        </article>
    );
}

type DetailsTableProps = {
    title: string;
    isEmpty: boolean;
    headers: string[];
    children: React.ReactNode;
};

function DetailsTable({
    title,
    isEmpty,
    headers,
    children,
}: DetailsTableProps) {
    return (
        <section className="details-section">
            <h3>{title}</h3>

            {isEmpty ? (
                <p>Brak danych.</p>
            ) : (
                <div className="details-table-wrapper">
                    <table className="details-table">
                        <thead>
                            <tr>
                                {headers.map((header) => (
                                    <th key={header}>{header}</th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>{children}</tbody>
                    </table>
                </div>
            )}
        </section>
    );
}
