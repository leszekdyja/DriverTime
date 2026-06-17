import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import { getVehicles, type Vehicle } from "../services/vehicleService";
import "../styles/drivers.css";

export default function VehiclesPage() {
    const [vehicles, setVehicles] = useState<Vehicle[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    const loadVehicles = useCallback(async () => {
        setIsLoading(true);
        setError("");

        try {
            setVehicles(await getVehicles());
        } catch (loadError) {
            setError(
                loadError instanceof Error
                    ? loadError.message
                    : "Wystąpił błąd podczas pobierania pojazdów.",
            );
        } finally {
            setIsLoading(false);
        }
    }, []);

    useEffect(() => {
        void loadVehicles();
    }, [loadVehicles]);

    return (
        <div className="drivers-page">
            <div className="drivers-heading">
                <div>
                    <h2>Pojazdy</h2>
                    <p>Lista aktywnych pojazdów przypisanych do firmy.</p>
                </div>
                <span className="drivers-count">{vehicles.length} pojazdów</span>
            </div>

            <section className="drivers-panel" style={{ marginTop: 28 }}>
                <div className="section-heading">
                    <h3>Aktywne pojazdy</h3>
                    <p>Pojazdy są tworzone automatycznie podczas importu plików DDD.</p>
                </div>

                {error ? (
                    <p className="drivers-message error" role="alert">
                        {error}
                    </p>
                ) : null}

                {isLoading ? (
                    <TableSkeleton rows={6} columns={4} />
                ) : vehicles.length === 0 && !error ? (
                    <EmptyState
                        title="Brak pojazdów"
                        description="Po imporcie plików DDD pojazdy pojawią się tutaj automatycznie."
                    />
                ) : null}

                {!isLoading && vehicles.length > 0 ? (
                    <div className="drivers-table-wrapper">
                        <table className="drivers-table">
                            <thead>
                                <tr>
                                    <th>Rejestracja</th>
                                    <th>VIN</th>
                                    <th>Status</th>
                                    <th></th>
                                </tr>
                            </thead>
                            <tbody>
                                {vehicles.map((vehicle) => (
                                    <tr key={vehicle.id}>
                                        <td>
                                            <Link className="driver-details-link" to={`/vehicles/${vehicle.id}`}>
                                                {vehicle.registrationNumber}
                                            </Link>
                                        </td>
                                        <td>{vehicle.vin || "Brak"}</td>
                                        <td>
                                            {vehicle.active ? (
                                                <StatusBadge label="Aktywny" tone="success" />
                                            ) : (
                                                <StatusBadge label="Nieaktywny" tone="neutral" />
                                            )}
                                        </td>
                                        <td>
                                            <Link className="driver-details-link" to={`/vehicles/${vehicle.id}`}>
                                                Szczegóły
                                            </Link>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : null}
            </section>
        </div>
    );
}
