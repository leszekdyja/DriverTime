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
import "../styles/drivers.css";

type DriverDto = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
    includeInPlanning: boolean;
    planningNoNightDuty: boolean;
    planningNoWeekends: boolean;
    planningNoSaturdays: boolean;
    planningNoHolidays: boolean;
    planningNoDaysOff: boolean;
};

type DriverMobileInviteDto = {
    driverId: string;
    driverFullName: string;
    token: string;
    apiBaseUrl: string;
    inviteLink: string;
    expiresAtUtc: string;
};

const driversApiUrl = `${API_URL}/api/drivers`;
const pageSize = 8;

export default function DriversPage() {
    const [drivers, setDrivers] = useState<DriverDto[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [message, setMessage] = useState("");
    const [isError, setIsError] = useState(false);
    const [search, setSearch] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const [driverToDelete, setDriverToDelete] = useState<DriverDto | null>(null);
    const [isDeleting, setIsDeleting] = useState(false);
    const [isCreatingInviteFor, setIsCreatingInviteFor] = useState<string | null>(null);
    const [mobileInvite, setMobileInvite] = useState<DriverMobileInviteDto | null>(null);
    const deferredSearch = useDeferredValue(search.trim().toLocaleLowerCase("pl-PL"));

    const filteredDrivers = useMemo(() => {
        if (!deferredSearch) return drivers;

        return drivers.filter((driver) =>
            driver.lastName.toLocaleLowerCase("pl-PL").includes(deferredSearch)
            || driver.firstName.toLocaleLowerCase("pl-PL").includes(deferredSearch)
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
                throw new Error("Nie udało się pobrać kierowców.");
            }

            setDrivers((await response.json()) as DriverDto[]);
        } catch {
            setIsError(true);
            setMessage("błąd podczas pobierania kierowców.");
        } finally {
            setIsLoading(false);
        }
    }, []);

    async function updateDriverPlanningSettings(driver: DriverDto, changes: Partial<Pick<DriverDto, "includeInPlanning" | "planningNoNightDuty" | "planningNoWeekends" | "planningNoSaturdays" | "planningNoHolidays" | "planningNoDaysOff">>) {
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
            setMessage("Kierowca został usunięty wraz z importami, aktywnościami i naruszeniami.");
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

    async function createMobileInvite(driver: DriverDto) {
        setMessage("");
        setIsError(false);
        setIsCreatingInviteFor(driver.id);

        try {
            const response = await apiFetch(`${driversApiUrl}/${driver.id}/mobile-invite`, {
                method: "POST",
            });

            if (!response.ok) {
                throw new Error("Nie udało się utworzyć linku do aplikacji.");
            }

            const invite = (await response.json()) as DriverMobileInviteDto;
            setMobileInvite(invite);

            if (navigator.clipboard) {
                await navigator.clipboard.writeText(invite.inviteLink);
                setMessage("Link do aplikacji został utworzony i skopiowany do schowka.");
            } else {
                setMessage("Link do aplikacji został utworzony.");
            }
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się utworzyć linku do aplikacji.");
        } finally {
            setIsCreatingInviteFor(null);
        }
    }

    async function shareMobileInvite() {
        if (!mobileInvite) return;

        const text = `DriverTime - konfiguracja aplikacji dla kierowcy ${mobileInvite.driverFullName}: ${mobileInvite.inviteLink}`;

        if (navigator.share) {
            await navigator.share({
                title: "DriverTime - aplikacja kierowcy",
                text,
            });
            return;
        }

        window.location.href = `mailto:?subject=${encodeURIComponent("DriverTime - aplikacja kierowcy")}&body=${encodeURIComponent(text)}`;
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
                    <div className="section-heading">
                        <h3>Lista kierowców</h3>
                        <p>Aktualna baza kierowców DriverTime.</p>
                    </div>

                    <div className="drivers-toolbar">
                        <label htmlFor="drivers-search">Szukaj kierowcy</label>
                        <input
                            id="drivers-search"
                            type="search"
                            placeholder="Nazwisko, imię lub numer karty"
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
                                        <th>Planowanie</th>
                                        <th>Blokady planowania</th>
                                        <th>Aplikacja</th>
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
                                                <button
                                                    className="driver-details-link"
                                                    type="button"
                                                    onClick={() => void createMobileInvite(driver)}
                                                    disabled={isCreatingInviteFor === driver.id}
                                                >
                                                    {isCreatingInviteFor === driver.id ? "Tworzenie..." : "Wyślij link"}
                                                </button>
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
                            Czy na pewno chcesz usunąć kierowcę {driverToDelete.lastName} {driverToDelete.firstName}? Usunięte zostaną również importy, aktywności i naruszenia tego kierowcy.
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
            {mobileInvite && (
                <div className="driver-delete-modal-backdrop" role="presentation" onClick={() => setMobileInvite(null)}>
                    <section
                        className="driver-delete-modal driver-mobile-invite-modal"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="driver-mobile-invite-title"
                        onClick={(event) => event.stopPropagation()}
                    >
                        <h3 id="driver-mobile-invite-title">Link do aplikacji kierowcy</h3>
                        <p>
                            Link zawiera jednorazową konfigurację aplikacji dla kierowcy {mobileInvite.driverFullName}.
                            Wygasa {new Date(mobileInvite.expiresAtUtc).toLocaleString("pl-PL")}.
                        </p>
                        <label>
                            Link konfiguracji
                            <textarea readOnly value={mobileInvite.inviteLink} rows={4} />
                        </label>
                        <div className="driver-delete-modal-actions">
                            <button
                                type="button"
                                onClick={() => void navigator.clipboard?.writeText(mobileInvite.inviteLink)}
                            >
                                Kopiuj
                            </button>
                            <button type="button" onClick={() => void shareMobileInvite()}>
                                Wyślij
                            </button>
                            <button type="button" onClick={() => setMobileInvite(null)}>
                                Zamknij
                            </button>
                        </div>
                    </section>
                </div>
            )}
        </div>
    );
}




