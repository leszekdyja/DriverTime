import { useEffect, useState } from "react";
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

export default function ImportDetailsPage() {
    const { id } = useParams<{ id: string }>();
    const [details, setDetails] = useState<DddImportDetails | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

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
                        : "Wystapil blad podczas pobierania szczegolow importu.",
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
                Wroc do listy importow
            </Link>

            <h2>Szczegoly importu DDD</h2>

            {isLoading && (
                <p className="status-message" role="status">
                    Ladowanie szczegolow importu...
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

                    <DetailsTable
                        title="Aktywnosci kierowcy"
                        isEmpty={details.driverActivities.length === 0}
                        headers={["Poczatek", "Koniec", "Aktywnosc"]}
                    >
                        {details.driverActivities.map((activity, index) => (
                            <tr key={`${activity.start}-${index}`}>
                                <td>{formatDate(activity.start)}</td>
                                <td>{formatDate(activity.end)}</td>
                                <td>{displayValue(activity.activity)}</td>
                            </tr>
                        ))}
                    </DetailsTable>

                    <DetailsTable
                        title="Wpisy krajow"
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
                        title="Uzycie pojazdow"
                        isEmpty={details.vehicleUses.length === 0}
                        headers={["Pojazd", "Poczatek", "Koniec"]}
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
