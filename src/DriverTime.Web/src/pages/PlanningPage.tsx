import { useEffect, useState, type FormEvent } from "react";

import { EmptyState, TableSkeleton } from "../components/UiStates";
import {
    createPlanningDuty,
    deletePlanningDuty,
    getPlanningDuty,
    getPlanningDuties,
    updatePlanningDuty,
    type PlanningDuty,
    type PlanningDutyDetails,
    type PlanningDutyPayload,
} from "../services/planningDutiesService";
import "../styles/drivers.css";
import "../styles/planning.css";

type PlanningDutyFormState = {
    dutyNumber: string;
    name: string;
    validFrom: string;
    vehicleRequirement: string;
    startTime: string;
    endTime: string;
    totalDurationMinutes: string;
    workMinutes: string;
    breakMinutes: string;
    drivingMinutes: string;
    distanceKm: string;
    notes: string;
};

const emptyForm: PlanningDutyFormState = {
    dutyNumber: "",
    name: "",
    validFrom: "",
    vehicleRequirement: "",
    startTime: "",
    endTime: "",
    totalDurationMinutes: "",
    workMinutes: "",
    breakMinutes: "",
    drivingMinutes: "",
    distanceKm: "",
    notes: "",
};

const dateFormatter = new Intl.DateTimeFormat("pl-PL", { dateStyle: "medium" });

function formatDate(value: string | null) {
    if (!value) return "Brak danych";

    const date = new Date(`${value}T00:00:00`);
    return Number.isNaN(date.getTime()) ? "Brak danych" : dateFormatter.format(date);
}

function formatTime(value: string | null) {
    return value ? value.slice(0, 5) : "Brak danych";
}

function formatMinutes(value: number | null) {
    return value === null || value === undefined ? "Brak danych" : `${value} min`;
}

function formatKm(value: number | null) {
    return value === null || value === undefined ? "Brak danych" : `${value.toLocaleString("pl-PL")} km`;
}

function toNumberOrNull(value: string) {
    if (value.trim() === "") return null;

    const normalized = value.replace(",", ".");
    const parsed = Number(normalized);

    return Number.isFinite(parsed) ? parsed : null;
}

function toIntegerOrNull(value: string) {
    const parsed = toNumberOrNull(value);

    return parsed === null ? null : Math.trunc(parsed);
}

function toPayload(form: PlanningDutyFormState): PlanningDutyPayload {
    return {
        dutyNumber: form.dutyNumber.trim(),
        name: form.name.trim(),
        validFrom: form.validFrom || null,
        vehicleRequirement: form.vehicleRequirement.trim() || null,
        startTime: form.startTime ? `${form.startTime}:00` : null,
        endTime: form.endTime ? `${form.endTime}:00` : null,
        totalDurationMinutes: toIntegerOrNull(form.totalDurationMinutes),
        workMinutes: toIntegerOrNull(form.workMinutes),
        breakMinutes: toIntegerOrNull(form.breakMinutes),
        drivingMinutes: toIntegerOrNull(form.drivingMinutes),
        distanceKm: toNumberOrNull(form.distanceKm),
        notes: form.notes.trim() || null,
        lines: [],
        stops: [],
    };
}

function fromDetails(details: PlanningDutyDetails): PlanningDutyFormState {
    return {
        dutyNumber: details.dutyNumber,
        name: details.name,
        validFrom: details.validFrom ?? "",
        vehicleRequirement: details.vehicleRequirement ?? "",
        startTime: details.startTime?.slice(0, 5) ?? "",
        endTime: details.endTime?.slice(0, 5) ?? "",
        totalDurationMinutes: details.totalDurationMinutes?.toString() ?? "",
        workMinutes: details.workMinutes?.toString() ?? "",
        breakMinutes: details.breakMinutes?.toString() ?? "",
        drivingMinutes: details.drivingMinutes?.toString() ?? "",
        distanceKm: details.distanceKm?.toString() ?? "",
        notes: details.notes ?? "",
    };
}

export default function PlanningPage() {
    const [duties, setDuties] = useState<PlanningDuty[]>([]);
    const [selectedDuty, setSelectedDuty] = useState<PlanningDutyDetails | null>(null);
    const [dutyToDelete, setDutyToDelete] = useState<PlanningDuty | null>(null);
    const [form, setForm] = useState<PlanningDutyFormState>(emptyForm);
    const [isEditing, setIsEditing] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [message, setMessage] = useState("");
    const [isMessageError, setIsMessageError] = useState(false);

    async function loadDuties() {
        setIsLoading(true);
        setMessage("");
        setIsMessageError(false);

        try {
            setDuties(await getPlanningDuties());
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Wystąpił błąd podczas pobierania służb.");
        } finally {
            setIsLoading(false);
        }
    }

    async function showDetails(id: string) {
        setMessage("");
        setIsMessageError(false);

        try {
            const details = await getPlanningDuty(id);
            setSelectedDuty(details);
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się pobrać szczegółów służby.");
        }
    }

    function startCreate() {
        setIsEditing(false);
        setSelectedDuty(null);
        setForm(emptyForm);
        setMessage("");
        setIsMessageError(false);
    }

    async function startEdit(duty: PlanningDuty) {
        setMessage("");
        setIsMessageError(false);

        try {
            const details = await getPlanningDuty(duty.id);
            setSelectedDuty(details);
            setForm(fromDetails(details));
            setIsEditing(true);
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się otworzyć edycji służby.");
        }
    }

    async function saveDuty(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setIsSaving(true);
        setMessage("");
        setIsMessageError(false);

        try {
            const payload = toPayload(form);
            const saved = isEditing && selectedDuty
                ? await updatePlanningDuty(selectedDuty.id, payload)
                : await createPlanningDuty(payload);

            setSelectedDuty(saved);
            setForm(fromDetails(saved));
            setIsEditing(true);
            await loadDuties();
            setMessage("Służba została zapisana.");
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać służby.");
        } finally {
            setIsSaving(false);
        }
    }

    async function confirmDelete() {
        if (!dutyToDelete) return;

        setIsDeleting(true);
        setMessage("");
        setIsMessageError(false);

        try {
            await deletePlanningDuty(dutyToDelete.id);
            if (selectedDuty?.id === dutyToDelete.id) {
                setSelectedDuty(null);
                setForm(emptyForm);
                setIsEditing(false);
            }
            setDutyToDelete(null);
            await loadDuties();
            setMessage("Służba została usunięta.");
        } catch (error) {
            setIsMessageError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się usunąć służby.");
        } finally {
            setIsDeleting(false);
        }
    }

    useEffect(() => {
        void loadDuties();
    }, []);

    return (
        <div className="drivers-page planning-page">
            <div className="drivers-heading">
                <div>
                    <h2>Planowanie</h2>
                    <p>Baza służb i tras do późniejszego importu oraz ręcznej edycji.</p>
                </div>
                <span className="drivers-count">{duties.length} służb</span>
            </div>

            {message ? (
                <p className={`drivers-message${isMessageError ? " error" : " success"}`} role={isMessageError ? "alert" : "status"}>
                    {message}
                </p>
            ) : null}

            <div className="planning-grid">
                <section className="drivers-panel">
                    <div className="section-heading planning-panel-heading">
                        <div>
                            <h3>Baza służb</h3>
                            <p>Lista służb planowania przypisanych do aktualnej firmy.</p>
                        </div>
                        <div className="driver-row-actions">
                            <button className="planning-secondary-button" type="button" onClick={() => void loadDuties()} disabled={isLoading}>
                                Odśwież
                            </button>
                            <button className="planning-primary-button" type="button" onClick={startCreate}>
                                Dodaj służbę
                            </button>
                        </div>
                    </div>

                    {isLoading ? <TableSkeleton rows={6} columns={7} /> : null}

                    {!isLoading && duties.length === 0 ? (
                        <EmptyState title="Brak służb" description="Dodaj pierwszą służbę ręcznie. Import z PDF pojawi się w kolejnym etapie." />
                    ) : null}

                    {!isLoading && duties.length > 0 ? (
                        <div className="drivers-table-wrapper">
                            <table className="drivers-table planning-table">
                                <thead>
                                    <tr>
                                        <th>Numer</th>
                                        <th>Nazwa</th>
                                        <th>Ważna od</th>
                                        <th>Godziny</th>
                                        <th>Czas pracy</th>
                                        <th>Kilometry</th>
                                        <th>Akcje</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {duties.map((duty) => (
                                        <tr key={duty.id}>
                                            <td>{duty.dutyNumber}</td>
                                            <td>{duty.name}</td>
                                            <td>{formatDate(duty.validFrom)}</td>
                                            <td>{formatTime(duty.startTime)}–{formatTime(duty.endTime)}</td>
                                            <td>{formatMinutes(duty.workMinutes)}</td>
                                            <td>{formatKm(duty.distanceKm)}</td>
                                            <td>
                                                <div className="driver-row-actions">
                                                    <button className="planning-link-button" type="button" onClick={() => void showDetails(duty.id)}>
                                                        Szczegóły
                                                    </button>
                                                    <button className="planning-link-button" type="button" onClick={() => void startEdit(duty)}>
                                                        Edytuj
                                                    </button>
                                                    <button className="driver-delete-button" type="button" onClick={() => setDutyToDelete(duty)}>
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

                <section className="driver-form planning-form-panel">
                    <div className="section-heading">
                        <h3>{isEditing ? "Edytuj służbę" : "Dodaj służbę"}</h3>
                        <p>Na tym etapie zapisujemy podstawowe dane służby. Linie i przystanki będą rozwijane przy imporcie PDF.</p>
                    </div>

                    <form className="planning-form" onSubmit={(event) => void saveDuty(event)}>
                        <label>Numer służby<input value={form.dutyNumber} onChange={(event) => setForm({ ...form, dutyNumber: event.target.value })} required maxLength={50} /></label>
                        <label>Nazwa<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required maxLength={200} /></label>
                        <label>Ważna od<input type="date" value={form.validFrom} onChange={(event) => setForm({ ...form, validFrom: event.target.value })} /></label>
                        <label>Wymagany pojazd<input value={form.vehicleRequirement} onChange={(event) => setForm({ ...form, vehicleRequirement: event.target.value })} maxLength={200} /></label>
                        <div className="planning-form-row">
                            <label>Start<input type="time" value={form.startTime} onChange={(event) => setForm({ ...form, startTime: event.target.value })} /></label>
                            <label>Koniec<input type="time" value={form.endTime} onChange={(event) => setForm({ ...form, endTime: event.target.value })} /></label>
                        </div>
                        <div className="planning-form-row">
                            <label>Czas całkowity min<input type="number" min="0" value={form.totalDurationMinutes} onChange={(event) => setForm({ ...form, totalDurationMinutes: event.target.value })} /></label>
                            <label>Czas pracy min<input type="number" min="0" value={form.workMinutes} onChange={(event) => setForm({ ...form, workMinutes: event.target.value })} /></label>
                        </div>
                        <div className="planning-form-row">
                            <label>Przerwy min<input type="number" min="0" value={form.breakMinutes} onChange={(event) => setForm({ ...form, breakMinutes: event.target.value })} /></label>
                            <label>Czas jazdy min<input type="number" min="0" value={form.drivingMinutes} onChange={(event) => setForm({ ...form, drivingMinutes: event.target.value })} /></label>
                        </div>
                        <label>Kilometry<input type="number" min="0" step="0.01" value={form.distanceKm} onChange={(event) => setForm({ ...form, distanceKm: event.target.value })} /></label>
                        <label>Uwagi<textarea value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} maxLength={4000} /></label>
                        <div className="driver-row-actions planning-form-actions">
                            <button type="submit" disabled={isSaving}>{isSaving ? "Zapisywanie..." : "Zapisz"}</button>
                            {isEditing ? <button className="planning-secondary-button" type="button" onClick={startCreate}>Nowa służba</button> : null}
                        </div>
                    </form>

                    <div className="planning-details-box">
                        <h4>Szczegóły służby</h4>
                        {selectedDuty ? (
                            <>
                                <p><strong>Linie:</strong> {selectedDuty.lines.length > 0 ? selectedDuty.lines.map((line) => line.lineCode).join(", ") : "Brak danych"}</p>
                                <p><strong>Przystanki:</strong> {selectedDuty.stops.length > 0 ? `${selectedDuty.stops.length} pozycji` : "Brak danych"}</p>
                            </>
                        ) : (
                            <p>Wybierz służbę z listy albo zapisz nową, aby zobaczyć szczegóły.</p>
                        )}
                    </div>
                </section>
            </div>

            {dutyToDelete ? (
                <div className="driver-delete-modal-backdrop" role="presentation" onClick={() => !isDeleting && setDutyToDelete(null)}>
                    <section className="driver-delete-modal" role="dialog" aria-modal="true" aria-labelledby="planning-delete-title" onClick={(event) => event.stopPropagation()}>
                        <h3 id="planning-delete-title">Usuń służbę</h3>
                        <p>Czy na pewno chcesz usunąć służbę {dutyToDelete.name}?</p>
                        <div className="driver-delete-modal-actions">
                            <button type="button" onClick={() => setDutyToDelete(null)} disabled={isDeleting}>Anuluj</button>
                            <button className="danger" type="button" onClick={() => void confirmDelete()} disabled={isDeleting}>{isDeleting ? "Usuwanie..." : "Usuń"}</button>
                        </div>
                    </section>
                </div>
            ) : null}
        </div>
    );
}

