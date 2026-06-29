import { useEffect, useMemo, useState, type FormEvent } from "react";

import { getDrivers, type Driver } from "../../services/driversService";
import { getPlanningDuties, type PlanningDuty } from "../../services/planningDutiesService";
import {
    createSchedule,
    deleteAssignment,
    deleteSchedule,
    getSchedule,
    getSchedules,
    updateSchedule,
    upsertAssignment,
    validateSchedule,
    type PlanningAssignment,
    type PlanningAssignmentType,
    type PlanningSchedule,
    type PlanningScheduleListItem,
    type PlanningScheduleValidation,
} from "../../services/planningSchedulesService";

const assignmentTypeLabels: Record<PlanningAssignmentType, string> = {
    Duty: "Służba",
    DayOff: "Wolne",
    Vacation: "Urlop",
    SickLeave: "Chorobowe",
    Training: "Szkolenie",
    Other: "Inne",
};

const assignmentTypes = Object.entries(assignmentTypeLabels) as Array<[PlanningAssignmentType, string]>;

type ScheduleForm = {
    name: string;
    year: string;
    month: string;
    notes: string;
};

type EditorState = {
    driverId: string;
    date: string;
    assignmentId: string | null;
    assignmentType: PlanningAssignmentType;
    planningDutyId: string;
    notes: string;
};

function getDefaultScheduleForm(): ScheduleForm {
    const today = new Date();
    return {
        name: `Grafik ${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`,
        year: today.getFullYear().toString(),
        month: (today.getMonth() + 1).toString(),
        notes: "",
    };
}

function getDriverName(driver: Driver) {
    const name = `${driver.firstName} ${driver.lastName}`.trim();
    return name || driver.cardNumber || "Kierowca";
}

function toDateKey(year: number, month: number, day: number) {
    return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

function getDaysInMonth(year: number, month: number) {
    const count = new Date(year, month, 0).getDate();
    return Array.from({ length: count }, (_, index) => index + 1);
}

function getAssignmentLabel(assignment?: PlanningAssignment) {
    if (!assignment) return "";
    if (assignment.assignmentType === "Duty") {
        return assignment.dutyNumber ? `${assignment.dutyNumber}${assignment.line ? ` / ${assignment.line}` : ""}` : "Służba";
    }

    return assignmentTypeLabels[assignment.assignmentType] ?? assignment.assignmentType;
}

export default function PlanningSchedulesTab() {
    const [schedules, setSchedules] = useState<PlanningScheduleListItem[]>([]);
    const [selectedSchedule, setSelectedSchedule] = useState<PlanningSchedule | null>(null);
    const [drivers, setDrivers] = useState<Driver[]>([]);
    const [duties, setDuties] = useState<PlanningDuty[]>([]);
    const [form, setForm] = useState<ScheduleForm>(getDefaultScheduleForm());
    const [isEditingSchedule, setIsEditingSchedule] = useState(false);
    const [editor, setEditor] = useState<EditorState | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isValidating, setIsValidating] = useState(false);
    const [validation, setValidation] = useState<PlanningScheduleValidation | null>(null);
    const [message, setMessage] = useState("");
    const [isError, setIsError] = useState(false);

    const days = useMemo(() => {
        if (!selectedSchedule) return [];
        return getDaysInMonth(selectedSchedule.year, selectedSchedule.month);
    }, [selectedSchedule]);

    const assignmentsByDriverAndDate = useMemo(() => {
        const map = new Map<string, PlanningAssignment>();
        selectedSchedule?.assignments.forEach((assignment) => {
            map.set(`${assignment.driverId}|${assignment.date}`, assignment);
        });
        return map;
    }, [selectedSchedule]);

    async function loadInitialData() {
        setIsLoading(true);
        setMessage("");
        setIsError(false);

        try {
            const [loadedSchedules, loadedDrivers, loadedDuties] = await Promise.all([
                getSchedules(),
                getDrivers(),
                getPlanningDuties(),
            ]);
            setSchedules(loadedSchedules);
            setDrivers(loadedDrivers);
            setDuties(loadedDuties);
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się pobrać danych grafików.");
        } finally {
            setIsLoading(false);
        }
    }

    async function selectSchedule(id: string) {
        setMessage("");
        setIsError(false);

        try {
            const schedule = await getSchedule(id);
            setSelectedSchedule(schedule);
            setIsEditingSchedule(true);
            setForm({
                name: schedule.name,
                year: schedule.year.toString(),
                month: schedule.month.toString(),
                notes: schedule.notes ?? "",
            });
            setEditor(null);
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się otworzyć grafiku.");
        }
    }

    function resetForm() {
        setForm(getDefaultScheduleForm());
        setSelectedSchedule(null);
        setIsEditingSchedule(false);
        setEditor(null);
        setValidation(null);
    }

    async function saveSchedule(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        setIsSaving(true);
        setMessage("");
        setIsError(false);

        const payload = {
            name: form.name.trim(),
            year: Number(form.year),
            month: Number(form.month),
            notes: form.notes.trim() || null,
        };

        try {
            const saved = isEditingSchedule && selectedSchedule
                ? await updateSchedule(selectedSchedule.id, payload)
                : await createSchedule(payload);
            const loadedSchedules = await getSchedules();
            setSchedules(loadedSchedules);
            setSelectedSchedule(saved);
            setIsEditingSchedule(true);
            setMessage("Grafik został zapisany.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać grafiku.");
        } finally {
            setIsSaving(false);
        }
    }

    async function removeSchedule(id: string) {
        if (!window.confirm("Czy na pewno chcesz usunąć ten grafik?")) return;

        setMessage("");
        setIsError(false);

        try {
            await deleteSchedule(id);
            if (selectedSchedule?.id === id) resetForm();
            setSchedules(await getSchedules());
            setMessage("Grafik został usunięty.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się usunąć grafiku.");
        }
    }

    function openEditor(driver: Driver, day: number) {
        if (!selectedSchedule) return;
        const date = toDateKey(selectedSchedule.year, selectedSchedule.month, day);
        const assignment = assignmentsByDriverAndDate.get(`${driver.id}|${date}`);

        setEditor({
            driverId: driver.id,
            date,
            assignmentId: assignment?.id ?? null,
            assignmentType: assignment?.assignmentType ?? "Duty",
            planningDutyId: assignment?.planningDutyId ?? "",
            notes: assignment?.notes ?? "",
        });
    }

    async function saveAssignment() {
        if (!selectedSchedule || !editor) return;

        setIsSaving(true);
        setMessage("");
        setIsError(false);

        try {
            await upsertAssignment(selectedSchedule.id, {
                date: editor.date,
                driverId: editor.driverId,
                assignmentType: editor.assignmentType,
                planningDutyId: editor.assignmentType === "Duty" ? editor.planningDutyId || null : null,
                notes: editor.notes.trim() || null,
            });
            setSelectedSchedule(await getSchedule(selectedSchedule.id));
            setEditor(null);
            setMessage("Przypisanie zostało zapisane.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać przypisania.");
        } finally {
            setIsSaving(false);
        }
    }

    async function removeAssignment() {
        if (!selectedSchedule || !editor?.assignmentId) return;

        setIsSaving(true);
        setMessage("");
        setIsError(false);

        try {
            await deleteAssignment(selectedSchedule.id, editor.assignmentId);
            setSelectedSchedule(await getSchedule(selectedSchedule.id));
            setEditor(null);
            setMessage("Przypisanie zostało usunięte.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się usunąć przypisania.");
        } finally {
            setIsSaving(false);
        }
    }

    async function checkScheduleValidation() {
        if (!selectedSchedule) return;

        setIsValidating(true);
        setMessage("");
        setIsError(false);

        try {
            const result = await validateSchedule(selectedSchedule.id);
            setValidation(result);
            setMessage("Walidacja grafiku została wykonana.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się sprawdzić grafiku.");
        } finally {
            setIsValidating(false);
        }
    }

    useEffect(() => {
        void loadInitialData();
    }, []);

    const selectedDriver = editor ? drivers.find((driver) => driver.id === editor.driverId) : null;

    return (
        <section className="drivers-panel planning-schedules-panel">
            <div className="section-heading planning-panel-heading">
                <div>
                    <h3>Grafiki miesięczne</h3>
                    <p>Podstawowy grafik kierowców na miesiąc. Automatyczne planowanie dodamy później.</p>
                </div>
                <button className="planning-primary-button" type="button" onClick={resetForm}>Nowy grafik</button>
            </div>

            {message ? <p className={`drivers-message${isError ? " error" : " success"}`}>{message}</p> : null}

            <div className="planning-schedules-layout">
                <aside className="planning-schedules-list">
                    <h4>Lista grafików</h4>
                    {isLoading ? <p className="drivers-status">Ładowanie grafików...</p> : null}
                    {!isLoading && schedules.length === 0 ? <p className="drivers-status">Brak grafików.</p> : null}
                    {schedules.map((schedule) => (
                        <button
                            key={schedule.id}
                            className={`planning-schedule-item${selectedSchedule?.id === schedule.id ? " active" : ""}`}
                            type="button"
                            onClick={() => void selectSchedule(schedule.id)}
                        >
                            <strong>{schedule.name}</strong>
                            <span>{schedule.month.toString().padStart(2, "0")}/{schedule.year} · {schedule.assignmentsCount} przypisań</span>
                        </button>
                    ))}
                </aside>

                <div className="planning-schedules-main">
                    <form className="planning-schedule-form" onSubmit={(event) => void saveSchedule(event)}>
                        <label>Nazwa<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></label>
                        <label>Rok<input type="number" min="2000" max="2100" value={form.year} onChange={(event) => setForm({ ...form, year: event.target.value })} required /></label>
                        <label>Miesiąc<input type="number" min="1" max="12" value={form.month} onChange={(event) => setForm({ ...form, month: event.target.value })} required /></label>
                        <label>Uwagi<input value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></label>
                        <div className="driver-row-actions">
                            <button type="submit" className="planning-primary-button" disabled={isSaving}>{isSaving ? "Zapisywanie..." : "Zapisz grafik"}</button>
                            {selectedSchedule ? <button type="button" className="driver-delete-button" onClick={() => void removeSchedule(selectedSchedule.id)}>Usuń grafik</button> : null}
                        </div>
                    </form>

                    {selectedSchedule ? (
                        <div className="planning-month-wrapper">
                            <div className="planning-validation-panel">
                                <div className="planning-validation-header">
                                    <div>
                                        <h4>Walidacja grafiku</h4>
                                        <p>Sprawdź podstawowe konflikty i braki w grafiku miesięcznym.</p>
                                    </div>
                                    <button className="planning-secondary-button" type="button" onClick={() => void checkScheduleValidation()} disabled={isValidating}>
                                        {isValidating ? "Sprawdzanie..." : "Sprawdź grafik"}
                                    </button>
                                </div>
                                {validation ? (
                                    <div className="planning-validation-results">
                                        <div className="planning-validation-summary">
                                            <span><strong>{validation.errorCount}</strong> błędów</span>
                                            <span><strong>{validation.warningCount}</strong> ostrzeżeń</span>
                                        </div>
                                        {validation.warnings.length === 0 ? (
                                            <p className="drivers-status">Nie znaleziono problemów w grafiku.</p>
                                        ) : (
                                            <ul className="planning-validation-list">
                                                {validation.warnings.map((warning, index) => (
                                                    <li key={`${warning.assignmentId ?? warning.code}-${index}`} className={`planning-validation-item ${warning.severity.toLowerCase()}`}>
                                                        <span className="planning-validation-severity">{warning.severity === "Error" ? "Błąd" : warning.severity === "Warning" ? "Ostrzeżenie" : "Info"}</span>
                                                        <span>{warning.date ?? "Brak daty"}</span>
                                                        <span>{warning.driverName ?? "Brak kierowcy"}</span>
                                                        <span>{warning.code}</span>
                                                        <strong>{warning.message}</strong>
                                                    </li>
                                                ))}
                                            </ul>
                                        )}
                                    </div>
                                ) : null}
                            </div>

                            <div className="planning-month-table-wrap">
                                <table className="planning-month-table">
                                    <thead>
                                        <tr>
                                            <th>Kierowca</th>
                                            {days.map((day) => <th key={day}>{day}</th>)}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {drivers.map((driver) => (
                                            <tr key={driver.id}>
                                                <th>{getDriverName(driver)}</th>
                                                {days.map((day) => {
                                                    const date = toDateKey(selectedSchedule.year, selectedSchedule.month, day);
                                                    const assignment = assignmentsByDriverAndDate.get(`${driver.id}|${date}`);
                                                    return (
                                                        <td key={date}>
                                                            <button type="button" onClick={() => openEditor(driver, day)} className={assignment ? "has-assignment" : ""}>
                                                                {getAssignmentLabel(assignment) || "+"}
                                                            </button>
                                                        </td>
                                                    );
                                                })}
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>

                            {editor ? (
                                <div className="planning-assignment-editor">
                                    <h4>{selectedDriver ? getDriverName(selectedDriver) : "Kierowca"} · {editor.date}</h4>
                                    <label>Typ
                                        <select value={editor.assignmentType} onChange={(event) => setEditor({ ...editor, assignmentType: event.target.value as PlanningAssignmentType })}>
                                            {assignmentTypes.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                                        </select>
                                    </label>
                                    {editor.assignmentType === "Duty" ? (
                                        <label>Służba
                                            <select value={editor.planningDutyId} onChange={(event) => setEditor({ ...editor, planningDutyId: event.target.value })}>
                                                <option value="">Wybierz służbę</option>
                                                {duties.map((duty) => <option key={duty.id} value={duty.id}>{duty.dutyNumber} · {duty.name}</option>)}
                                            </select>
                                        </label>
                                    ) : null}
                                    <label>Notatka<textarea value={editor.notes} onChange={(event) => setEditor({ ...editor, notes: event.target.value })} /></label>
                                    <div className="driver-row-actions">
                                        <button className="planning-primary-button" type="button" onClick={() => void saveAssignment()} disabled={isSaving}>Zapisz</button>
                                        {editor.assignmentId ? <button className="driver-delete-button" type="button" onClick={() => void removeAssignment()} disabled={isSaving}>Usuń przypisanie</button> : null}
                                        <button className="planning-secondary-button" type="button" onClick={() => setEditor(null)}>Anuluj</button>
                                    </div>
                                </div>
                            ) : null}
                        </div>
                    ) : <p className="drivers-status">Wybierz grafik z listy albo utwórz nowy.</p>}
                </div>
            </div>
        </section>
    );
}




