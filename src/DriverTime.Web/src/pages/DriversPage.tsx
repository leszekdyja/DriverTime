import {
    useCallback,
    useDeferredValue,
    useEffect,
    useMemo,
    useState,
} from "react";
import { Link } from "react-router-dom";

import Pagination from "../components/Pagination";
import { EmptyState, TableSkeleton } from "../components/UiStates";
import { API_URL } from "../config/api";
import { apiFetch } from "../services/apiClient";
import { getOperatingCompanies, type OperatingCompany } from "../services/operatingCompaniesService";
import "../styles/drivers.css";

type DriverDto = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
    operatingCompanyId: string | null;
    operatingCompanyName: string | null;
    includeInPlanning: boolean;
    isActive: boolean;
    planningNoNightDuty: boolean;
    planningNoWeekends: boolean;
    planningNoSaturdays: boolean;
    planningNoHolidays: boolean;
    planningNoDaysOff: boolean;
};

const driversApiUrl = `${API_URL}/api/drivers`;
const pageSize = 8;

export default function DriversPage() {
    const [drivers, setDrivers] = useState<DriverDto[]>([]);
    const [companies, setCompanies] = useState<OperatingCompany[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [message, setMessage] = useState("");
    const [isError, setIsError] = useState(false);
    const [search, setSearch] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const [driverToDelete, setDriverToDelete] = useState<DriverDto | null>(null);
    const [isDeleting, setIsDeleting] = useState(false);
    const [deletedDrivers, setDeletedDrivers] = useState<Array<{ id: string; firstName: string; lastName: string; operatingCompanyName: string | null; restoreUntilUtc: string; canRestore: boolean }>>([]);
    const [isTrashLoading, setIsTrashLoading] = useState(false);
    const [restoringId, setRestoringId] = useState<string | null>(null);

    async function loadDeletedDrivers() {
        setIsTrashLoading(true);
        try {
            const response = await apiFetch(`${driversApiUrl}/deleted`);
            if (!response.ok) throw new Error("Nie udało się pobrać usuniętych kierowców.");
            setDeletedDrivers(await response.json());
        } catch (error) {
            setIsError(true); setMessage(error instanceof Error ? error.message : "Błąd pobierania usuniętych kierowców.");
        } finally { setIsTrashLoading(false); }
    }

    async function restoreDriver(id: string) {
        setRestoringId(id); setMessage("");
        try {
            const response = await apiFetch(`${driversApiUrl}/${id}/restore`, { method: "POST" });
            if (!response.ok) throw new Error(response.status === 410 ? "Minął miesięczny termin przywrócenia." : "Nie udało się przywrócić kierowcy.");
            await Promise.all([loadDrivers(), loadDeletedDrivers()]);
            setIsError(false); setMessage("Kierowca został przywrócony. Jego historia i przypisania zostały zachowane.");
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Błąd przywracania."); }
        finally { setRestoringId(null); }
    }
    const deferredSearch = useDeferredValue(search.trim().toLocaleLowerCase("pl-PL"));

    const filteredDrivers = useMemo(() => {
        if (!deferredSearch) return drivers;

        return drivers.filter((driver) =>
            driver.lastName.toLocaleLowerCase("pl-PL").includes(deferredSearch)
            || driver.firstName.toLocaleLowerCase("pl-PL").includes(deferredSearch)
            || driver.cardNumber.toLocaleLowerCase("pl-PL").includes(deferredSearch)
            || driver.operatingCompanyName?.toLocaleLowerCase("pl-PL").includes(deferredSearch),
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
            const [response, loadedCompanies] = await Promise.all([
                apiFetch(driversApiUrl),
                getOperatingCompanies(),
            ]);

            if (!response.ok) {
                throw new Error("Nie udało się pobrać kierowców.");
            }

            setDrivers((await response.json()) as DriverDto[]);
            setCompanies(loadedCompanies);
        } catch {
            setIsError(true);
            setMessage("błąd podczas pobierania kierowców.");
        } finally {
            setIsLoading(false);
        }
    }, []);

    async function assignCompany(driver: DriverDto, operatingCompanyId: string | null) {
        setMessage(""); setIsError(false);
        try {
            const response = await apiFetch(`${driversApiUrl}/${driver.id}/operating-company`, {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ operatingCompanyId }),
            });
            if (!response.ok) throw new Error("Nie udało się przypisać kierowcy do firmy.");
            const saved = await response.json() as DriverDto;
            setDrivers(current => current.map(item => item.id === saved.id ? saved : item));
            setMessage(operatingCompanyId ? "Kierowca został przypisany do firmy." : "Usunięto przypisanie kierowcy do firmy.");
        } catch (assignError) {
            setIsError(true);
            setMessage(assignError instanceof Error ? assignError.message : "Nie udało się przypisać kierowcy do firmy.");
        }
    }

    async function updateDriverPlanningSettings(driver: DriverDto, changes: Partial<Pick<DriverDto, "isActive" | "includeInPlanning" | "planningNoNightDuty" | "planningNoWeekends" | "planningNoSaturdays" | "planningNoHolidays" | "planningNoDaysOff">>) {
        setMessage("");
        setIsError(false);
        const updated = { ...driver, ...changes };
        setDrivers((current) => current.map((item) => item.id === driver.id ? updated : item));

        try {
            const response = await apiFetch(`${driversApiUrl}/${driver.id}/planning`, {
                method: "PATCH",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    includeInPlanning: updated.includeInPlanning,
                    isActive: updated.isActive,
                    planningNoNightDuty: updated.planningNoNightDuty,
                    planningNoWeekends: updated.planningNoWeekends,
                    planningNoSaturdays: updated.planningNoSaturdays,
                    planningNoHolidays: updated.planningNoHolidays,
                    planningNoDaysOff: updated.planningNoDaysOff,
                }),
            });

            if (!response.ok) {
                throw new Error("Nie udało się zapisać ustawienia planowania.");
            }

            const saved = (await response.json()) as DriverDto;
            setDrivers((current) => current.map((item) => item.id === saved.id ? saved : item));
            setMessage("Ustawienie planowania kierowcy zostało zapisane.");
        } catch (error) {
            setDrivers((current) => current.map((item) => item.id === driver.id ? driver : item));
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać ustawienia planowania.");
        }
    }
    async function deleteDriver() {
        if (!driverToDelete) return;

        setIsDeleting(true);
        setMessage("");
        setIsError(false);

        try {
            const response = await apiFetch(`${driversApiUrl}/${driverToDelete.id}`, {
                method: "DELETE",
            });

            if (response.status === 404) {
                throw new Error("Nie znaleziono kierowcy w Twojej firmie.");
            }

            if (!response.ok) {
                throw new Error("Nie udało się usunąć kierowcy.");
            }

            setDriverToDelete(null);
            await loadDrivers();
            await loadDeletedDrivers();
            setMessage("Kierowca został przeniesiony do usuniętych. Możesz go przywrócić przez miesiąc. Historia pozostaje zachowana.");
        } catch (deleteError) {
            setIsError(true);
            setMessage(
                deleteError instanceof Error
                    ? deleteError.message
                    : "Wystąpił błąd podczas usuwania kierowcy.",
            );
        } finally {
            setIsDeleting(false);
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
                    <p>Kierowcy utworzeni automatycznie z kart DDD lub dodani w ewidencji.</p>
                </div>
                <span className="drivers-count">{drivers.length} kierowców</span>
            </div>

            <section className="drivers-panel">
                    <details onToggle={(event) => { if (event.currentTarget.open) void loadDeletedDrivers(); }}>
                        <summary>Usunięci kierowcy — przywracanie przez miesiąc</summary>
                        <p>Usunięcie ukrywa profil i wyłącza go z nowego planowania. Nie kasuje odczytów ani istniejących obsad. Po miesiącu przywracanie jest blokowane; historia nie jest automatycznie kasowana.</p>
                        {isTrashLoading ? <p>Wczytywanie…</p> : deletedDrivers.length === 0 ? <p>Brak usuniętych kierowców.</p> : (
                            <ul>{deletedDrivers.map((driver) => <li key={driver.id}>
                                <strong>{driver.lastName} {driver.firstName}</strong> — {driver.operatingCompanyName || "Bez przypisanej firmy"}. Przywrócenie do: {new Intl.DateTimeFormat("pl-PL", {dateStyle:"medium",timeStyle:"short",timeZone:"Europe/Warsaw"}).format(new Date(driver.restoreUntilUtc))}.
                                <button type="button" disabled={!driver.canRestore || restoringId !== null} onClick={() => void restoreDriver(driver.id)}>{driver.canRestore ? (restoringId === driver.id ? "Przywracanie…" : "Przywróć kierowcę") : "Termin przywrócenia minął"}</button>
                            </li>)}</ul>
                        )}
                    </details>
                    <div className="section-heading">
                        <h3>Lista kierowców</h3>
                        <p>Aktualna baza kierowców DriverTime.</p>
                    </div>

                    <div className="drivers-toolbar">
                        <label htmlFor="drivers-search">Szukaj kierowcy</label>
                        <input
                            id="drivers-search"
                            type="search"
                            placeholder="Nazwisko, imię, numer karty lub firma"
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
                            <TableSkeleton rows={6} columns={8} />
                        ) : null
                    ) : drivers.length === 0 ? (
                        <EmptyState
                            title="Brak kierowców"
                            description="Dodaj kierowcę w zakładce Ewidencja lub zaimportuj plik DDD, aby utworzyć go automatycznie."
                        />
                    ) : filteredDrivers.length === 0 ? (
                        <EmptyState
                            title="Brak wynikow"
                            description="Zmień nazwisko lub numer karty wpisany w wyszukiwarce."
                        />
                    ) : null}

                    {drivers.length > 0 && filteredDrivers.length > 0 && (
                        <div className={isLoading ? "drivers-content is-refreshing" : "drivers-content"} aria-busy={isLoading}>
                            <div className="drivers-table-wrapper">
                                <table className="drivers-table">
                                <thead>
                                    <tr>
                                        <th>Nazwisko</th>
                                        <th>Imie</th>
                                        <th>Numer karty</th>
                                        <th>Wazna do</th>
                                        <th>Kraj wydania</th>
                                        <th>Firma</th>
                                        <th>Planowanie</th>
                                        <th>Blokady planowania</th>
                                        <th>Status kierowcy</th>
                                        <th></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {visibleDrivers.map((driver) => (
                                        <tr key={driver.id}>
                                            <td>{driver.lastName}</td>
                                            <td>{driver.firstName}</td>
                                            <td>{driver.cardNumber}</td>
                                            <td>
                                                {driver.cardExpiryDate
                                                    ? new Date(driver.cardExpiryDate).toLocaleDateString("pl-PL")
                                                    : "Brak danych"}
                                            </td>
                                            <td>{driver.cardIssuingCountry || "Brak danych"}</td>
                                            <td>
                                                <select value={driver.operatingCompanyId ?? ""} onChange={event => void assignCompany(driver, event.target.value || null)}>
                                                    <option value="">Bez przypisania</option>
                                                    {companies.filter(company => company.active || company.id === driver.operatingCompanyId).map(company => <option key={company.id} value={company.id}>{company.name}{company.active ? "" : " (nieaktywna)"}</option>)}
                                                </select>
                                            </td>
                                            <td>
                                                <label className="driver-table-toggle">
                                                    <input
                                                        type="checkbox"
                                                        checked={driver.includeInPlanning}
                                                        onChange={(event) => void updateDriverPlanningSettings(driver, { includeInPlanning: event.target.checked })}
                                                    />
                                                    <span>{driver.includeInPlanning ? "Tak" : "Nie"}</span>
                                                </label>
                                            </td>
                                            <td>
                                                <div className="driver-row-actions">
                                                    <label><input type="checkbox" checked={driver.planningNoNightDuty} onChange={(event) => void updateDriverPlanningSettings(driver, { planningNoNightDuty: event.target.checked })} /> Nie RN</label>
                                                    <label><input type="checkbox" checked={driver.planningNoWeekends} onChange={(event) => void updateDriverPlanningSettings(driver, { planningNoWeekends: event.target.checked })} /> Bez weekendów</label>
                                                    <label><input type="checkbox" checked={driver.planningNoSaturdays} onChange={(event) => void updateDriverPlanningSettings(driver, { planningNoSaturdays: event.target.checked })} /> Bez sobót</label>
                                                    <label><input type="checkbox" checked={driver.planningNoHolidays} onChange={(event) => void updateDriverPlanningSettings(driver, { planningNoHolidays: event.target.checked })} /> Bez świąt</label>
                                                    <label><input type="checkbox" checked={driver.planningNoDaysOff} onChange={(event) => void updateDriverPlanningSettings(driver, { planningNoDaysOff: event.target.checked })} /> Bez dni wolnych</label>
                                                </div>
                                            </td>
                                            <td>
                                                <label className="driver-table-toggle" title="Nieaktywny kierowca zachowuje historię, ale nie jest planowany automatycznie i nie generuje alertów odczytu ani ważności karty.">
                                                    <input type="checkbox" checked={!driver.isActive}
                                                        onChange={event => void updateDriverPlanningSettings(driver, { isActive: !event.target.checked })}
                                                        aria-label={`Kierowca nieaktywny: ${driver.lastName} ${driver.firstName}`} />
                                                    <span>Nieaktywny</span>
                                                </label>
                                            </td>
                                            <td>
                                                <div className="driver-row-actions">
                                                <Link className="driver-details-link" to={`/drivers/${driver.id}`}>
                                                    Szczegóły
                                                </Link>
                                                    <button
                                                        className="driver-delete-button"
                                                        type="button"
                                                        onClick={() => setDriverToDelete(driver)}
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
                            <Pagination
                                currentPage={currentPage}
                                pageSize={pageSize}
                                totalItems={filteredDrivers.length}
                                onPageChange={setCurrentPage}
                            />
                        </div>
                    )}
            </section>

            {driverToDelete && (
                <div className="driver-delete-modal-backdrop" role="presentation" onClick={() => !isDeleting && setDriverToDelete(null)}>
                    <section
                        className="driver-delete-modal"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="driver-delete-title"
                        onClick={(event) => event.stopPropagation()}
                    >
                        <h3 id="driver-delete-title">Usuń kierowcę</h3>
                        <p>
                            Czy chcesz przenieść kierowcę {driverToDelete.lastName} {driverToDelete.firstName} do usuniętych? Przez miesiąc możesz go przywrócić. Odczyty, aktywności, naruszenia i dotychczasowe obsady pozostaną zachowane.
                        </p>
                        <div className="driver-delete-modal-actions">
                            <button
                                type="button"
                                onClick={() => setDriverToDelete(null)}
                                disabled={isDeleting}
                            >
                                Anuluj
                            </button>
                            <button
                                className="danger"
                                type="button"
                                onClick={() => void deleteDriver()}
                                disabled={isDeleting}
                            >
                                {isDeleting ? "Usuwanie..." : "Usuń kierowcę"}
                            </button>
                        </div>
                    </section>
                </div>
            )}
        </div>
    );
}




