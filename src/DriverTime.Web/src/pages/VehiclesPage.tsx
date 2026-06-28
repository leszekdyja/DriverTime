import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";

import StatusBadge from "../components/StatusBadge";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import { deleteVehicle, getVehicles, type Vehicle } from "../services/vehicleService";
import "../styles/drivers.css";

export default function VehiclesPage() {
    const [vehicles, setVehicles] = useState<Vehicle[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");
    const [isMessageError, setIsMessageError] = useState(false);
    const [vehicleToDelete, setVehicleToDelete] = useState<Vehicle | null>(null);
    const [isDeleting, setIsDeleting] = useState(false);

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

    async function confirmDeleteVehicle() {
        if (!vehicleToDelete) return;

        setIsDeleting(true);
        setMessage("");
        setIsMessageError(false);

        try {
            await deleteVehicle(vehicleToDelete.id);
            const deletedRegistration = vehicleToDelete.registrationNumber;
            setVehicleToDelete(null);
            await loadVehicles();
            setMessage(`Pojazd ${deletedRegistration} został usunięty. Historia użyć i importów została zachowana.`);
        } catch (deleteError) {
            setIsMessageError(true);
            setMessage(
                deleteError instanceof Error
                    ? deleteError.message
                    : "Wystąpił błąd podczas usuwania pojazdu.",
            );
        } finally {
            setIsDeleting(false);
        }
    }

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

                {message ? (
                    <p className={`drivers-message${isMessageError ? " error" : " success"}`} role={isMessageError ? "alert" : "status"}>
                        {message}
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
                                            <div className="driver-row-actions">
                                                <Link className="driver-details-link" to={`/vehicles/${vehicle.id}`}>
                                                    Szczegóły
                                                </Link>
                                                <button
                                                    className="driver-delete-button"
                                                    type="button"
                                                    onClick={() => setVehicleToDelete(vehicle)}
                                                >
                                                    Usuń
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                ) : null}
            </section>

            {vehicleToDelete && (
                <div className="driver-delete-modal-backdrop" role="presentation" onClick={() => !isDeleting && setVehicleToDelete(null)}>
                    <section
                        className="driver-delete-modal"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="vehicle-delete-title"
                        onClick={(event) => event.stopPropagation()}
                    >
                        <h3 id="vehicle-delete-title">Usuń pojazd</h3>
                        <p>Czy na pewno chcesz usunąć pojazd {vehicleToDelete.registrationNumber}?</p>
                        <div className="driver-delete-modal-actions">
                            <button
                                type="button"
                                onClick={() => setVehicleToDelete(null)}
                                disabled={isDeleting}
                            >
                                Anuluj
                            </button>
                            <button
                                className="danger"
                                type="button"
                                onClick={() => void confirmDeleteVehicle()}
                                disabled={isDeleting}
                            >
                                {isDeleting ? "Usuwanie..." : "Usuń pojazd"}
                            </button>
                        </div>
                    </section>
                </div>
            )}
        </div>
    );
}
