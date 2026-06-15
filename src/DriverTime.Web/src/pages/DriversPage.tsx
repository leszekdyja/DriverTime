import { useCallback, useEffect, useState } from "react";

type DriverDto = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
};

type CreateDriverDto = {
    firstName: string;
    lastName: string;
    cardNumber: string;
};

const API_URL = "https://localhost:65468/api/drivers";

function DriversPage() {
    const [drivers, setDrivers] = useState<DriverDto[]>([]);

    const [form, setForm] = useState<CreateDriverDto>({
        firstName: "",
        lastName: "",
        cardNumber: "",
    });

    const [isLoading, setIsLoading] = useState(false);

    const [message, setMessage] = useState("");

    const loadDrivers = useCallback(async () => {
        setIsLoading(true);

        setMessage("");

        try {
            const response = await fetch(API_URL);

            if (!response.ok) {
                throw new Error("Nie udalo sie pobrac kierowcow.");
            }

            const data = await response.json();

            setDrivers(data);
        }
        catch {
            setMessage("Blad podczas pobierania kierowcow.");
        }
        finally {
            setIsLoading(false);
        }
    }, []);

    async function addDriver(
        event: React.SyntheticEvent<HTMLFormElement>
    ) {
        event.preventDefault();

        setMessage("");

        if (
            !form.firstName ||
            !form.lastName ||
            !form.cardNumber
        ) {
            setMessage("Uzupelnij wszystkie pola.");
            return;
        }

        try {
            const response = await fetch(API_URL, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                },
                body: JSON.stringify(form),
            });

            if (!response.ok) {
                throw new Error("Nie udalo sie dodac kierowcy.");
            }

            setForm({
                firstName: "",
                lastName: "",
                cardNumber: "",
            });

            setMessage("Kierowca zostal dodany.");

            await loadDrivers();
        }
        catch {
            setMessage("Blad podczas dodawania kierowcy.");
        }
    }

    useEffect(() => {
        void loadDrivers();
    }, [loadDrivers]);

    return (
        <div style={{ padding: "20px" }}>
            <h1>Kierowcy</h1>

            <form
                onSubmit={addDriver}
                style={{
                    marginBottom: "20px",
                    border: "1px solid #cccccc",
                    padding: "20px",
                }}
            >
                <h2>Dodaj kierowce</h2>

                <div style={{ marginBottom: "10px" }}>
                    <label>Imie</label>

                    <br />

                    <input
                        type="text"
                        value={form.firstName}
                        onChange={(e) =>
                            setForm({
                                ...form,
                                firstName: e.target.value,
                            })
                        }
                    />
                </div>

                <div style={{ marginBottom: "10px" }}>
                    <label>Nazwisko</label>

                    <br />

                    <input
                        type="text"
                        value={form.lastName}
                        onChange={(e) =>
                            setForm({
                                ...form,
                                lastName: e.target.value,
                            })
                        }
                    />
                </div>

                <div style={{ marginBottom: "10px" }}>
                    <label>Numer karty kierowcy</label>

                    <br />

                    <input
                        type="text"
                        value={form.cardNumber}
                        onChange={(e) =>
                            setForm({
                                ...form,
                                cardNumber: e.target.value,
                            })
                        }
                    />
                </div>

                <button type="submit">
                    Dodaj kierowce
                </button>
            </form>

            {message && (
                <p>{message}</p>
            )}

            <h2>Lista kierowcow</h2>

            {isLoading && (
                <p>Ladowanie...</p>
            )}

            {!isLoading && drivers.length === 0 && (
                <p>Brak kierowcow.</p>
            )}

            {!isLoading && drivers.length > 0 && (
                <table
                    border={1}
                    cellPadding={10}
                >
                    <thead>
                        <tr>
                            <th>Imie</th>
                            <th>Nazwisko</th>
                            <th>Numer karty</th>
                        </tr>
                    </thead>

                    <tbody>
                        {drivers.map((driver) => (
                            <tr key={driver.id}>
                                <td>{driver.firstName}</td>
                                <td>{driver.lastName}</td>
                                <td>{driver.cardNumber}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </div>
    );
}

export default DriversPage;