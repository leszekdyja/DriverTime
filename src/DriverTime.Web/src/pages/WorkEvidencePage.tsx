import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";

import { getDrivers, type Driver } from "../services/driversService";
import {
    createWorkEvidenceEntry,
    deleteWorkEvidenceEntry,
    getDriverWorkEvidence,
    updateWorkEvidenceEntry,
    type WorkEvidenceActivityType,
    type WorkEvidenceEntry,
    type WorkEvidenceEntryRequest,
    type WorkEvidenceMonth,
} from "../services/workEvidenceService";
import "../styles/workEvidence.css";

const activityOptions: Array<{ value: WorkEvidenceActivityType; label: string }> = [
    { value: "Driving", label: "Jazda" },
    { value: "OtherWork", label: "Inna praca" },
    { value: "Availability", label: "Dyspozycyjność" },
    { value: "BreakRest", label: "Przerwa/odpoczynek" },
    { value: "Vacation", label: "Urlop" },
    { value: "SickLeave", label: "Chorobowe" },
    { value: "DayOff", label: "Dzień wolny" },
    { value: "OtherAbsence", label: "Inna nieobecność" },
];

type EntryForm = {
    id: string | null;
    date: string;
    startTime: string;
    endTime: string;
    endsNextDay: boolean;
    activityType: WorkEvidenceActivityType;
    vehicleRegistration: string;
    countryCode: string;
    distanceKm: string;
    description: string;
};

const today = new Date();
const initialMonth = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;

export default function WorkEvidencePage() {
    const [drivers, setDrivers] = useState<Driver[]>([]);
    const [selectedDriverId, setSelectedDriverId] = useState("");
    const [selectedMonth, setSelectedMonth] = useState(initialMonth);
    const [evidence, setEvidence] = useState<WorkEvidenceMonth | null>(null);
    const [form, setForm] = useState<EntryForm>(() => createEmptyForm(today.toISOString().slice(0, 10)));
    const [isLoading, setIsLoading] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [message, setMessage] = useState("");
    const [isError, setIsError] = useState(false);

    const selectedDriver = useMemo(
        () => drivers.find((driver) => driver.id === selectedDriverId) ?? null,
        [drivers, selectedDriverId],
    );

    const monthParts = useMemo(() => {
        const [year, month] = selectedMonth.split("-").map(Number);
        return { year, month };
    }, [selectedMonth]);

    const loadEvidence = useCallback(async () => {
        if (!selectedDriverId) {
            setEvidence(null);
            return;
        }

        setIsLoading(true);
        setMessage("");
        setIsError(false);

        try {
            const result = await getDriverWorkEvidence(
                selectedDriverId,
                monthParts.year,
                monthParts.month,
            );
            setEvidence(result);
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się pobrać ewidencji.");
        } finally {
            setIsLoading(false);
        }
    }, [monthParts.month, monthParts.year, selectedDriverId]);

    useEffect(() => {
        async function loadDrivers() {
            setIsLoading(true);

            try {
                const result = await getDrivers();
                setDrivers(result);
                setSelectedDriverId((current) => current || result[0]?.id || "");
            } catch (error) {
                setIsError(true);
                setMessage(error instanceof Error ? error.message : "Nie udało się pobrać kierowców.");
            } finally {
                setIsLoading(false);
            }
        }

        void loadDrivers();
    }, []);

    useEffect(() => {
        void loadEvidence();
    }, [loadEvidence]);

    useEffect(() => {
        const firstDay = `${selectedMonth}-01`;
        setForm((current) => ({ ...current, date: firstDay }));
    }, [selectedMonth]);

    async function submitEntry(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (!selectedDriverId) {
            setIsError(true);
            setMessage("Wybierz kierowcę.");
            return;
        }

        setIsSaving(true);
        setMessage("");
        setIsError(false);

        try {
            const request = buildRequest(form);

            if (form.id) {
                await updateWorkEvidenceEntry(form.id, request);
                setMessage("Wpis ewidencji został zaktualizowany.");
            } else {
                await createWorkEvidenceEntry(selectedDriverId, request);
                setMessage("Wpis ewidencji został dodany.");
            }

            setForm(createEmptyForm(form.date));
            await loadEvidence();
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać wpisu.");
        } finally {
            setIsSaving(false);
        }
    }

    async function removeEntry(entry: WorkEvidenceEntry) {
        if (!window.confirm("Usunąć wpis ewidencji?")) {
            return;
        }

        setMessage("");
        setIsError(false);

        try {
            await deleteWorkEvidenceEntry(entry.id);
            await loadEvidence();
            setMessage("Wpis ewidencji został usunięty.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się usunąć wpisu.");
        }
    }

    function editEntry(entry: WorkEvidenceEntry) {
        setForm({
            id: entry.id,
            date: entry.date,
            startTime: normalizeTime(entry.startTime),
            endTime: normalizeTime(entry.endTime),
            endsNextDay: entry.endsNextDay,
            activityType: entry.activityType,
            vehicleRegistration: entry.vehicleRegistration ?? "",
            countryCode: entry.countryCode ?? "",
            distanceKm: entry.distanceKm?.toString() ?? "",
            description: entry.description ?? "",
        });
    }

    return (
        <div className="work-evidence-page">
            <div className="work-evidence-heading">
                <div>
                    <h2>Ewidencja czasu kierowcy</h2>
                    <p>Ręczne wpisywanie wielu aktywności dziennie i miesięczny wydruk raportu.</p>
                </div>
                <button className="secondary-button" type="button" onClick={() => window.print()} disabled={!evidence}>
                    Drukuj raport
                </button>
            </div>

            <section className="work-evidence-controls">
                <label>
                    Kierowca
                    <select value={selectedDriverId} onChange={(event) => setSelectedDriverId(event.target.value)}>
                        {drivers.map((driver) => (
                            <option key={driver.id} value={driver.id}>
                                {driver.lastName} {driver.firstName} ({driver.cardNumber})
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Miesiąc
                    <input
                        type="month"
                        value={selectedMonth}
                        onChange={(event) => setSelectedMonth(event.target.value)}
                    />
                </label>
            </section>

            {message && <div className={`work-evidence-message${isError ? " error" : ""}`}>{message}</div>}

            <div className="work-evidence-layout">
                <form className="work-evidence-form" onSubmit={submitEntry}>
                    <div className="section-heading">
                        <h3>{form.id ? "Edytuj aktywność" : "Dodaj aktywność"}</h3>
                        <p>Każdy dzień może mieć kilka kolejnych wpisów aktywności.</p>
                    </div>

                    <label>
                        Data
                        <input
                            type="date"
                            value={form.date}
                            onChange={(event) => setForm((current) => ({ ...current, date: event.target.value }))}
                            required
                        />
                    </label>

                    <div className="work-evidence-time-row">
                        <label>
                            Od
                            <input
                                type="time"
                                value={form.startTime}
                                onChange={(event) => setForm((current) => ({ ...current, startTime: event.target.value }))}
                                required
                            />
                        </label>
                        <label>
                            Do
                            <input
                                type="time"
                                value={form.endTime}
                                onChange={(event) => setForm((current) => ({ ...current, endTime: event.target.value }))}
                                required
                            />
                        </label>
                    </div>

                    <label className="work-evidence-checkbox">
                        <input
                            type="checkbox"
                            checked={form.endsNextDay}
                            onChange={(event) => setForm((current) => ({ ...current, endsNextDay: event.target.checked }))}
                        />
                        Kończy się następnego dnia
                    </label>

                    <label>
                        Aktywność
                        <select
                            value={form.activityType}
                            onChange={(event) => setForm((current) => ({
                                ...current,
                                activityType: event.target.value as WorkEvidenceActivityType,
                            }))}
                        >
                            {activityOptions.map((option) => (
                                <option key={option.value} value={option.value}>{option.label}</option>
                            ))}
                        </select>
                    </label>

                    <label>
                        Pojazd
                        <input
                            type="text"
                            value={form.vehicleRegistration}
                            onChange={(event) => setForm((current) => ({ ...current, vehicleRegistration: event.target.value }))}
                            placeholder="np. DW 1234A"
                        />
                    </label>

                    <div className="work-evidence-time-row">
                        <label>
                            Kraj
                            <input
                                type="text"
                                value={form.countryCode}
                                onChange={(event) => setForm((current) => ({ ...current, countryCode: event.target.value }))}
                                placeholder="PL"
                            />
                        </label>
                        <label>
                            Kilometry
                            <input
                                type="number"
                                min="0"
                                step="0.1"
                                value={form.distanceKm}
                                onChange={(event) => setForm((current) => ({ ...current, distanceKm: event.target.value }))}
                            />
                        </label>
                    </div>

                    <label>
                        Opis
                        <textarea
                            value={form.description}
                            onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
                            rows={3}
                        />
                    </label>

                    <div className="work-evidence-form-actions">
                        <button type="submit" disabled={isSaving || !selectedDriverId}>
                            {isSaving ? "Zapisywanie..." : form.id ? "Zapisz zmiany" : "Dodaj wpis"}
                        </button>
                        {form.id && (
                            <button
                                type="button"
                                className="secondary-button"
                                onClick={() => setForm(createEmptyForm(form.date))}
                            >
                                Anuluj
                            </button>
                        )}
                    </div>
                </form>

                <section className="work-evidence-report" id="work-evidence-print-area">
                    <div className="work-evidence-report-header">
                        <div>
                            <h3>Raport miesięczny</h3>
                            <p>
                                {selectedDriver?.firstName} {selectedDriver?.lastName} / {selectedMonth}
                            </p>
                        </div>
                        {evidence && (
                            <div className="work-evidence-summary">
                                <span>Jazda: {formatMinutes(evidence.summary.drivingMinutes)}</span>
                                <span>Praca: {formatMinutes(evidence.summary.otherWorkMinutes)}</span>
                                <span>Dysp.: {formatMinutes(evidence.summary.availabilityMinutes)}</span>
                                <span>Razem: {formatMinutes(evidence.summary.totalTrackedMinutes)}</span>
                            </div>
                        )}
                    </div>

                    {isLoading && <div className="work-evidence-empty">Ładowanie ewidencji...</div>}

                    {!isLoading && !evidence && (
                        <div className="work-evidence-empty">Wybierz kierowcę, aby zobaczyć ewidencję.</div>
                    )}

                    {!isLoading && evidence && (
                        <table className="work-evidence-table">
                            <thead>
                                <tr>
                                    <th>Dzień</th>
                                    <th>Aktywności</th>
                                    <th>Jazda</th>
                                    <th>Praca</th>
                                    <th>Dysp.</th>
                                    <th>Nieob.</th>
                                    <th className="work-evidence-actions-col">Akcje</th>
                                </tr>
                            </thead>
                            <tbody>
                                {evidence.days.map((day) => (
                                    <tr key={day.date}>
                                        <td>{formatDate(day.date)}</td>
                                        <td>
                                            {day.entries.length === 0 ? (
                                                <span className="work-evidence-muted">brak wpisów</span>
                                            ) : (
                                                <div className="work-evidence-entry-list">
                                                    {day.entries.map((entry) => (
                                                        <div key={entry.id} className="work-evidence-entry">
                                                            <strong>
                                                                {normalizeTime(entry.startTime)}-{normalizeTime(entry.endTime)}
                                                                {entry.endsNextDay ? " +1" : ""} · {activityLabel(entry.activityType)}
                                                            </strong>
                                                            <span>
                                                                {formatMinutes(entry.durationMinutes)}
                                                                {entry.vehicleRegistration ? ` · ${entry.vehicleRegistration}` : ""}
                                                                {entry.distanceKm ? ` · ${entry.distanceKm} km` : ""}
                                                            </span>
                                                            {entry.description && <small>{entry.description}</small>}
                                                        </div>
                                                    ))}
                                                </div>
                                            )}
                                        </td>
                                        <td>{formatMinutes(day.drivingMinutes)}</td>
                                        <td>{formatMinutes(day.otherWorkMinutes)}</td>
                                        <td>{formatMinutes(day.availabilityMinutes)}</td>
                                        <td>{formatMinutes(day.absenceMinutes)}</td>
                                        <td className="work-evidence-actions-col">
                                            {day.entries.map((entry) => (
                                                <div key={entry.id} className="work-evidence-entry-actions">
                                                    <button type="button" onClick={() => editEntry(entry)}>Edytuj</button>
                                                    <button type="button" onClick={() => void removeEntry(entry)}>Usuń</button>
                                                </div>
                                            ))}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </section>
            </div>
        </div>
    );
}

function createEmptyForm(date: string): EntryForm {
    return {
        id: null,
        date,
        startTime: "08:00",
        endTime: "16:00",
        endsNextDay: false,
        activityType: "Driving",
        vehicleRegistration: "",
        countryCode: "PL",
        distanceKm: "",
        description: "",
    };
}

function buildRequest(form: EntryForm): WorkEvidenceEntryRequest {
    return {
        date: form.date,
        startTime: form.startTime,
        endTime: form.endTime,
        endsNextDay: form.endsNextDay,
        activityType: form.activityType,
        vehicleRegistration: form.vehicleRegistration.trim() || null,
        countryCode: form.countryCode.trim() || null,
        distanceKm: form.distanceKm === "" ? null : Number(form.distanceKm),
        description: form.description.trim() || null,
    };
}

function normalizeTime(value: string) {
    return value.slice(0, 5);
}

function activityLabel(value: WorkEvidenceActivityType) {
    return activityOptions.find((option) => option.value === value)?.label ?? value;
}

function formatDate(value: string) {
    return new Intl.DateTimeFormat("pl-PL", {
        day: "2-digit",
        month: "2-digit",
        weekday: "short",
    }).format(new Date(`${value}T12:00:00`));
}

function formatMinutes(minutes: number) {
    if (!minutes) return "-";

    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;

    return `${hours}:${String(rest).padStart(2, "0")}`;
}
