import {
    useCallback,
    useDeferredValue,
    useEffect,
    useMemo,
    useState,
    type FormEvent,
} from "react";
import { Link } from "react-router-dom";

import Pagination from "../components/Pagination";
import { EmptyState, TableSkeleton } from "../components/UiStates";
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
const pageSize = 8;

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
    const [search, setSearch] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const deferredSearch = useDeferredValue(search.trim().toLocaleLowerCase("pl-PL"));

    const filteredDrivers = useMemo(() => {
        if (!deferredSearch) return drivers;

        return drivers.filter((driver) =>
            driver.lastName.toLocaleLowerCase("pl-PL").includes(deferredSearch)
            || driver.cardNumber.toLocaleLowerCase("pl-PL").includes(deferredSearch),
        );
    }, [deferredSearch, drivers]);

    const totalPages = Math.max(1, Math.ceil(filteredDrivers.length / pageSize));
    const visibleDrivers = useMemo(() => {
        const start = (currentPage - 1) * pageSize;
        return filteredDrivers.slice(start, start + pageSize);
    }, [currentPage, filteredDrivers]);

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

    useEffect(() => {
        setCurrentPage(1);
    }, [deferredSearch]);

    useEffect(() => {
        if (currentPage > totalPages) setCurrentPage(totalPages);
    }, [currentPage, totalPages]);

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

                    <div className="drivers-toolbar">
                        <label htmlFor="drivers-search">Szukaj kierowcy</label>
                        <input
                            id="drivers-search"
                            type="search"
                            placeholder="Nazwisko lub numer karty"
                            value={search}
                            onChange={(event) => setSearch(event.target.value)}
                        />
                        {search && (
                            <button type="button" onClick={() => setSearch("")}>
                                Wyczysc
                            </button>
                        )}
                    </div>

                    {message && (
                        <p className={`drivers-message${isError ? " error" : " success"}`}>
                            {message}
                        </p>
                    )}

                    {isLoading ? (
                        drivers.length === 0 ? (
                            <TableSkeleton rows={6} columns={6} />
                        ) : null
                    ) : drivers.length === 0 ? (
                        <EmptyState
                            title="Brak kierowcow"
                            description="Dodaj kierowce recznie lub zaimportuj plik DDD, aby utworzyc go automatycznie."
                        />
                    ) : filteredDrivers.length === 0 ? (
                        <EmptyState
                            title="Brak wynikow"
                            description="Zmien nazwisko lub numer karty wpisany w wyszukiwarce."
                        />
                    ) : null}

                    {drivers.length > 0 && filteredDrivers.length > 0 && (
                        <div className={isLoading ? "drivers-content is-refreshing" : "drivers-content"} aria-busy={isLoading}>
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
                                    {visibleDrivers.map((driver) => (
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
                            <Pagination
                                currentPage={currentPage}
                                pageSize={pageSize}
                                totalItems={filteredDrivers.length}
                                onPageChange={setCurrentPage}
                            />
                        </div>
                    )}
                </section>
            </div>
        </div>
    );
}
