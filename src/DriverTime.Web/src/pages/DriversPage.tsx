import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";

import { API_URL } from "../config/api";
import { apiFetch } from "../services/apiClient";
import "../styles/drivers.css";

type DriverDto = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
};

type CreateDriverDto = {
    firstName: string;
    lastName: string;
    cardNumber: string;
};

const driversApiUrl = `${API_URL}/api/drivers`;

export default function DriversPage() {
    const [drivers, setDrivers] = useState<DriverDto[]>([]);
    const [form, setForm] = useState<CreateDriverDto>({
        firstName: "",
        lastName: "",
        cardNumber: "",
    });
    const [isLoading, setIsLoading] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState("");
    const [isError, setIsError] = useState(false);

    const loadDrivers = useCallback(async () => {
        setIsLoading(true);
        setMessage("");

        try {
            const response = await apiFetch(driversApiUrl);

            if (!response.ok) {
                throw new Error("Nie udalo sie pobrac kierowcow.");
            }

            setDrivers((await response.json()) as DriverDto[]);
        } catch {
            setIsError(true);
            setMessage("Blad podczas pobierania kierowcow.");
        } finally {
            setIsLoading(false);
        }
    }, []);

    async function addDriver(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setMessage("");
        setIsError(false);

        if (!form.firstName || !form.lastName || !form.cardNumber) {
            setIsError(true);
            setMessage("Uzupelnij wszystkie pola.");
            return;
        }

        setIsSaving(true);

        try {
            const response = await apiFetch(driversApiUrl, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(form),
            });

            if (!response.ok) {
                throw new Error("Nie udalo sie dodac kierowcy.");
            }

            setForm({ firstName: "", lastName: "", cardNumber: "" });
            await loadDrivers();
            setMessage("Kierowca zostal dodany.");
        } catch {
            setIsError(true);
            setMessage("Blad podczas dodawania kierowcy.");
        } finally {
            setIsSaving(false);
        }
    }

    useEffect(() => {
        void loadDrivers();
    }, [loadDrivers]);

    return (
        <div className="drivers-page">
            <div className="drivers-heading">
                <div>
                    <h2>Kierowcy</h2>
                    <p>Zarzadzaj kierowcami i numerami kart kierowcow.</p>
                </div>
                <span className="drivers-count">{drivers.length} kierowcow</span>
            </div>

            <div className="drivers-grid">
                <form className="driver-form" onSubmit={addDriver}>
                    <div className="section-heading">
                        <h3>Dodaj kierowce</h3>
                        <p>Wprowadz podstawowe dane nowego kierowcy.</p>
                    </div>

                    <label>
                        Imie
                        <input
                            type="text"
                            value={form.firstName}
                            onChange={(event) =>
                                setForm({ ...form, firstName: event.target.value })
                            }
                        />
                    </label>

                    <label>
                        Nazwisko
                        <input
                            type="text"
                            value={form.lastName}
                            onChange={(event) =>
                                setForm({ ...form, lastName: event.target.value })
                            }
                        />
                    </label>

                    <label>
                        Numer karty kierowcy
                        <input
                            type="text"
                            value={form.cardNumber}
                            onChange={(event) =>
                                setForm({ ...form, cardNumber: event.target.value })
                            }
                        />
                    </label>

                    <button type="submit" disabled={isSaving}>
                        {isSaving ? "Zapisywanie..." : "Dodaj kierowce"}
                    </button>
                </form>

                <section className="drivers-panel">
                    <div className="section-heading">
                        <h3>Lista kierowcow</h3>
                        <p>Aktualna baza kierowcow DriverTime.</p>
                    </div>

                    {message && (
                        <p className={`drivers-message${isError ? " error" : " success"}`}>
                            {message}
                        </p>
                    )}

                    {isLoading ? (
                        <p className="drivers-status" role="status">
                            Ladowanie kierowcow...
                        </p>
                    ) : drivers.length === 0 ? (
                        <p>Brak kierowcow.</p>
                    ) : (
                        <div className="drivers-table-wrapper">
                            <table className="drivers-table">
                                <thead>
                                    <tr>
                                        <th>Imie</th>
                                        <th>Nazwisko</th>
                                        <th>Numer karty</th>
                                        <th>Wazna do</th>
                                        <th>Kraj wydania</th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {drivers.map((driver) => (
                                        <tr key={driver.id}>
                                            <td>{driver.firstName}</td>
                                            <td>{driver.lastName}</td>
                                            <td>{driver.cardNumber}</td>
                                            <td>
                                                {driver.cardExpiryDate
                                                    ? new Date(driver.cardExpiryDate).toLocaleDateString("pl-PL")
                                                    : "Brak danych"}
                                            </td>
                                            <td>{driver.cardIssuingCountry || "Brak danych"}</td>
                                            <td>
                                                <Link className="driver-details-link" to={`/drivers/${driver.id}`}>
                                                    Szczegoly
                                                </Link>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </section>
            </div>
        </div>
    );
}
