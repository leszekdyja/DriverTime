import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";

import { getDrivers, type Driver } from "../../services/driversService";
import { getPlanningDuties, updatePlanningDutyActiveDays, type PlanningDuty } from "../../services/planningDutiesService";
import {
    autoGeneratePlanning,
    previewAutoGeneratePlanning,
    createManualPlanningAssignment,
    createPlanningAssignmentRule,
    createPlanningDriverAvailability,
    createSchedule,
    deletePlanningAssignment,
    deletePlanningAssignmentRule,
    deletePlanningDriverAvailability,
    deleteSchedule,
    getPlanningAssignments,
    getPlanningAssignmentRules,
    getPlanningDriverAvailability,
    getSchedule,
    getSchedules,
    updatePlanningAssignment,
    updateSchedule,
    validateSchedule,
    type PlanningAssignment,
    type PlanningAssignmentListItem,
    type PlanningAutoGenerateResult,
    type PlanningDriverDutyRule,
    type PlanningDriverAvailability,
    type PlanningDriverAvailabilityType,
    type PlanningSchedule,
    type PlanningScheduleListItem,
    type PlanningScheduleValidation,
} from "../../services/planningSchedulesService";
import { buildPlanningMonthlyGrid, formatDriverLastFirst, toPlanningDateKey } from "./buildPlanningMonthlyGrid";
import { getPlanningDutyMask, planningAllWeekMask, planningDutyDayOptions, planningWeekdaysMask, planningWeekendMask, togglePlanningDutyDay } from "./planningDutyDayConfig";
import { PlanningAssignmentEditorModal, type PlanningAssignmentEditorState, type PlanningManualEntryCode } from "./PlanningAssignmentEditorModal";
import { PlanningGenerationSummary } from "./PlanningGenerationSummary";
import { PlanningMonthNavigation } from "./PlanningMonthNavigation";
import { PlanningMonthlyGrid } from "./PlanningMonthlyGrid";

type RuleForm = { driverId: string; dutyId: string; type: "Forbidden" | "Preferred"; validFrom: string; validTo: string; notes: string; };
type AvailabilityForm = { driverId: string; dateFrom: string; dateTo: string; type: PlanningDriverAvailabilityType; note: string; };
type ScheduleForm = { name: string; year: string; month: string; notes: string; };


const availabilityTypeLabels: Record<PlanningDriverAvailabilityType, string> = {
    Available: "Dostępny",
    Vacation: "Urlop",
    DayOff: "Dzień wolny",
    SickLeave: "Chorobowe",
    Unavailable: "Niedostępny",
};
const availabilityTypes = Object.entries(availabilityTypeLabels) as Array<[PlanningDriverAvailabilityType, string]>;

function toDateInputValue(date: Date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}
function monthRange(year: number, month: number) {
    return { dateFrom: toPlanningDateKey(year, month, 1), dateTo: toPlanningDateKey(year, month, new Date(year, month, 0).getDate()) };
}
function getCurrentMonth() { const today = new Date(); return { year: today.getFullYear(), month: today.getMonth() + 1 }; }
function getDefaultRuleForm(): RuleForm { return { driverId: "", dutyId: "", type: "Forbidden", validFrom: "", validTo: "", notes: "" }; }
function getDefaultAvailabilityForm(): AvailabilityForm { const today = new Date(); return { driverId: "", dateFrom: toDateInputValue(today), dateTo: toDateInputValue(today), type: "Unavailable", note: "" }; }
function getDefaultScheduleForm(): ScheduleForm {
    const today = new Date();
    return { name: `Grafik ${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`, year: today.getFullYear().toString(), month: (today.getMonth() + 1).toString(), notes: "" };
}
function formatDateTime(value: string | null) {
    if (!value) return "-";
    return new Date(value).toLocaleString("pl-PL", { year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit" });
}
function formatStatus(status: string) {
    if (status === "Generated") return "Automatyczne";
    if (status === "Manual") return "Ręczne";
    if (status === "Conflict") return "Konflikt";
    return status;
}
function getEditorEntryCode(assignment?: PlanningAssignment | null): PlanningManualEntryCode {
    if (!assignment) return "Duty";
    if (assignment.assignmentType === "Vacation") return "Vacation";
    if (assignment.assignmentType === "SickLeave") return "SickLeave";
    if (assignment.assignmentType === "Other" && assignment.notes?.toLowerCase().includes("niedost")) return "Unavailable";
    const code = assignment.dutyNumber?.toUpperCase();
    if (code === "RN" || code === "R" || code === "R2" || code === "WG" || code === "W") return code;
    return "Duty";
}

export default function PlanningSchedulesTab() {
    const currentMonth = getCurrentMonth();
    const [schedules, setSchedules] = useState<PlanningScheduleListItem[]>([]);
    const [selectedSchedule, setSelectedSchedule] = useState<PlanningSchedule | null>(null);
    const [selectedYear, setSelectedYear] = useState(currentMonth.year);
    const [selectedMonth, setSelectedMonth] = useState(currentMonth.month);
    const [drivers, setDrivers] = useState<Driver[]>([]);
    const [duties, setDuties] = useState<PlanningDuty[]>([]);
    const [isSavingDutyDays, setIsSavingDutyDays] = useState(false);
    const [form, setForm] = useState<ScheduleForm>(getDefaultScheduleForm());
    const [availabilityForm, setAvailabilityForm] = useState<AvailabilityForm>(getDefaultAvailabilityForm());
    const [ruleForm, setRuleForm] = useState<RuleForm>(getDefaultRuleForm());
    const [autoAssignments, setAutoAssignments] = useState<PlanningAssignmentListItem[]>([]);
    const [driverAvailability, setDriverAvailability] = useState<PlanningDriverAvailability[]>([]);
    const [assignmentRules, setAssignmentRules] = useState<PlanningDriverDutyRule[]>([]);
    const [lastGenerationResult, setLastGenerationResult] = useState<PlanningAutoGenerateResult | null>(null);
    const [isEditingSchedule, setIsEditingSchedule] = useState(false);
    const [editor, setEditor] = useState<PlanningAssignmentEditorState | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isSaving, setIsSaving] = useState(false);
    const [isValidating, setIsValidating] = useState(false);
    const [isGenerating, setIsGenerating] = useState(false);
    const [isSavingAvailability, setIsSavingAvailability] = useState(false);
    const [isSavingRule, setIsSavingRule] = useState(false);
    const [validation, setValidation] = useState<PlanningScheduleValidation | null>(null);
    const [message, setMessage] = useState("");
    const [isError, setIsError] = useState(false);

    const selectedRange = useMemo(() => monthRange(selectedYear, selectedMonth), [selectedYear, selectedMonth]);
    const holidayDates = useMemo(() => lastGenerationResult?.monthlyCalendars.filter((calendar) => calendar.year === selectedYear && calendar.month === selectedMonth).flatMap((calendar) => calendar.holidays.map((holiday) => holiday.date)) ?? [], [lastGenerationResult, selectedYear, selectedMonth]);
    const planningEnabledDriversCount = useMemo(() => drivers.filter((driver) => driver.includeInPlanning).length, [drivers]);
    const grid = useMemo(() => buildPlanningMonthlyGrid(drivers, selectedSchedule?.assignments ?? [], selectedYear, selectedMonth, holidayDates), [drivers, selectedSchedule, selectedYear, selectedMonth, holidayDates]);
    const assignmentsByDriverAndDate = useMemo(() => { const map = new Map<string, PlanningAssignment>(); selectedSchedule?.assignments.forEach((assignment) => map.set(`${assignment.driverId}|${assignment.date}`, assignment)); return map; }, [selectedSchedule]);
    const selectedDriver = editor ? drivers.find((driver) => driver.id === editor.driverId) ?? null : null;
    const selectedAssignment = editor ? assignmentsByDriverAndDate.get(`${editor.driverId}|${editor.date}`) ?? null : null;

    async function refreshMonth(year = selectedYear, month = selectedMonth, scheduleList = schedules) {
        const range = monthRange(year, month);
        const [loadedAssignments, loadedAvailability] = await Promise.all([getPlanningAssignments(range.dateFrom, range.dateTo), getPlanningDriverAvailability(range.dateFrom, range.dateTo)]);
        setAutoAssignments(loadedAssignments);
        setDriverAvailability(loadedAvailability);
        const monthSchedule = scheduleList.find((schedule) => schedule.year === year && schedule.month === month) ?? null;
        if (monthSchedule) {
            const fullSchedule = await getSchedule(monthSchedule.id);
            setSelectedSchedule(fullSchedule);
            setIsEditingSchedule(true);
            setForm({ name: fullSchedule.name, year: fullSchedule.year.toString(), month: fullSchedule.month.toString(), notes: fullSchedule.notes ?? "" });
        } else {
            setSelectedSchedule(null);
            setIsEditingSchedule(false);
            setForm({ name: `Grafik ${year}-${String(month).padStart(2, "0")}`, year: year.toString(), month: month.toString(), notes: "" });
        }
    }

    async function loadInitialData() {
        setIsLoading(true); setMessage(""); setIsError(false);
        try {
            const [loadedSchedules, loadedDrivers, loadedDuties, loadedRules] = await Promise.all([getSchedules(), getDrivers(), getPlanningDuties(), getPlanningAssignmentRules()]);
            setSchedules(loadedSchedules); setDrivers(loadedDrivers); setDuties(loadedDuties); setAssignmentRules(loadedRules);
            await refreshMonth(selectedYear, selectedMonth, loadedSchedules);
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się pobrać danych grafików."); }
        finally { setIsLoading(false); }
    }

    async function changeMonth(year: number, month: number) {
        const normalized = new Date(year, month - 1, 1);
        const nextYear = normalized.getFullYear();
        const nextMonth = normalized.getMonth() + 1;
        setSelectedYear(nextYear); setSelectedMonth(nextMonth); setEditor(null); setValidation(null); setMessage(""); setIsError(false);
        await refreshMonth(nextYear, nextMonth);
    }

    async function selectSchedule(id: string) {
        setMessage(""); setIsError(false);
        try {
            const schedule = await getSchedule(id);
            setSelectedSchedule(schedule); setSelectedYear(schedule.year); setSelectedMonth(schedule.month); setIsEditingSchedule(true);
            setForm({ name: schedule.name, year: schedule.year.toString(), month: schedule.month.toString(), notes: schedule.notes ?? "" });
            setEditor(null);
            await refreshMonth(schedule.year, schedule.month);
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się otworzyć grafiku."); }
    }

    function resetForm() {
        setForm({ name: `Grafik ${selectedYear}-${String(selectedMonth).padStart(2, "0")}`, year: selectedYear.toString(), month: selectedMonth.toString(), notes: "" });
        setSelectedSchedule(null); setIsEditingSchedule(false); setEditor(null); setValidation(null);
    }

    async function saveSchedule(event: FormEvent<HTMLFormElement>) {
        event.preventDefault(); setIsSaving(true); setMessage(""); setIsError(false);
        const payload = { name: form.name.trim(), year: Number(form.year), month: Number(form.month), notes: form.notes.trim() || null };
        try {
            const saved = isEditingSchedule && selectedSchedule ? await updateSchedule(selectedSchedule.id, payload) : await createSchedule(payload);
            const loadedSchedules = await getSchedules();
            setSchedules(loadedSchedules); setSelectedYear(saved.year); setSelectedMonth(saved.month); setSelectedSchedule(saved); setIsEditingSchedule(true);
            await refreshMonth(saved.year, saved.month, loadedSchedules);
            setMessage("Grafik został zapisany.");
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się zapisać grafiku."); }
        finally { setIsSaving(false); }
    }

    async function removeSchedule(id: string) {
        if (!window.confirm("Czy na pewno chcesz usunąć ten grafik?")) return;
        setMessage(""); setIsError(false);
        try { await deleteSchedule(id); const loadedSchedules = await getSchedules(); setSchedules(loadedSchedules); await refreshMonth(selectedYear, selectedMonth, loadedSchedules); setMessage("Grafik został usunięty."); }
        catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się usunąć grafiku."); }
    }

    function openEditor(driverId: string, date: string) {
        const assignment = assignmentsByDriverAndDate.get(`${driverId}|${date}`) ?? null;
        setEditor({ driverId, date, assignmentId: assignment?.id ?? null, entryCode: getEditorEntryCode(assignment), planningDutyId: assignment?.planningDutyId ?? "", notes: assignment?.notes ?? "" });
    }

    async function saveAssignment() {
        if (!editor) return;
        setIsSaving(true); setMessage(""); setIsError(false);
        try {
            const payload = { date: editor.date, driverId: editor.driverId, dutyId: editor.entryCode === "Duty" ? editor.planningDutyId || null : null, entryCode: editor.entryCode === "Duty" ? null : editor.entryCode, notes: editor.notes.trim() || null };
            if (editor.assignmentId) await updatePlanningAssignment(editor.assignmentId, payload); else await createManualPlanningAssignment(payload);
            const loadedSchedules = await getSchedules(); setSchedules(loadedSchedules); await refreshMonth(selectedYear, selectedMonth, loadedSchedules);
            setEditor(null); setMessage("Komórka grafiku została zapisana.");
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się zapisać wpisu."); }
        finally { setIsSaving(false); }
    }

    async function removeAssignment() {
        if (!editor?.assignmentId) return;
        setIsSaving(true); setMessage(""); setIsError(false);
        try { await deletePlanningAssignment(editor.assignmentId); const loadedSchedules = await getSchedules(); setSchedules(loadedSchedules); await refreshMonth(selectedYear, selectedMonth, loadedSchedules); setEditor(null); setMessage("Wpis został usunięty."); }
        catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się usunąć wpisu."); }
        finally { setIsSaving(false); }
    }

    async function loadAssignmentRules() { setMessage(""); setIsError(false); try { setAssignmentRules(await getPlanningAssignmentRules()); } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się pobrać reguł planowania."); } }
    async function saveAssignmentRule() {
        setIsSavingRule(true); setMessage(""); setIsError(false);
        try { await createPlanningAssignmentRule({ driverId: ruleForm.driverId, dutyId: ruleForm.dutyId, type: ruleForm.type, validFrom: ruleForm.validFrom || null, validTo: ruleForm.validTo || null, notes: ruleForm.notes.trim() || null }); setRuleForm(getDefaultRuleForm()); setAssignmentRules(await getPlanningAssignmentRules()); setMessage("Reguła planowania została zapisana."); }
        catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się zapisać reguły planowania."); }
        finally { setIsSavingRule(false); }
    }
    async function removeAssignmentRule(id: string) { setIsSavingRule(true); setMessage(""); setIsError(false); try { await deletePlanningAssignmentRule(id); setAssignmentRules(await getPlanningAssignmentRules()); setMessage("Reguła planowania została usunięta."); } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się usunąć reguły planowania."); } finally { setIsSavingRule(false); } }
    async function loadDriverAvailability() { setMessage(""); setIsError(false); try { setDriverAvailability(await getPlanningDriverAvailability(selectedRange.dateFrom, selectedRange.dateTo)); } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się pobrać dostępności kierowców."); } }
    async function saveDriverAvailability() {
        setIsSavingAvailability(true); setMessage(""); setIsError(false);
        try { await createPlanningDriverAvailability({ driverId: availabilityForm.driverId, dateFrom: availabilityForm.dateFrom, dateTo: availabilityForm.dateTo, type: availabilityForm.type, note: availabilityForm.note.trim() || null }); setAvailabilityForm(getDefaultAvailabilityForm()); setDriverAvailability(await getPlanningDriverAvailability(selectedRange.dateFrom, selectedRange.dateTo)); setMessage("Dostępność kierowcy została zapisana."); }
        catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się zapisać dostępności kierowcy."); }
        finally { setIsSavingAvailability(false); }
    }
    async function removeDriverAvailability(id: string) { setIsSavingAvailability(true); setMessage(""); setIsError(false); try { await deletePlanningDriverAvailability(id); setDriverAvailability(await getPlanningDriverAvailability(selectedRange.dateFrom, selectedRange.dateTo)); setMessage("Wpis dostępności został usunięty."); } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się usunąć dostępności kierowcy."); } finally { setIsSavingAvailability(false); } }
    function getDutyMask(duty: PlanningDuty) {
        return getPlanningDutyMask(duty.activeDaysMask);
    }

    function setDutyLocalDays(dutyId: string, activeDaysMask: number, includeHolidays: boolean) {
        setDuties((current) => current.map((duty) => duty.id === dutyId ? { ...duty, activeDaysMask, includeHolidays } : duty));
    }

    async function saveDutyDays(duty: PlanningDuty, activeDaysMask: number, includeHolidays: boolean) {
        setIsSavingDutyDays(true);
        setMessage("");
        setIsError(false);
        try {
            const saved = await updatePlanningDutyActiveDays(duty.id, { activeDaysMask, includeHolidays });
            setDutyLocalDays(duty.id, getPlanningDutyMask(saved.activeDaysMask), saved.includeHolidays);
            setMessage("Dni wykonywania służby zostały zapisane.");
        } catch (error) {
            setIsError(true);
            setMessage(error instanceof Error ? error.message : "Nie udało się zapisać dni wykonywania służby.");
        } finally {
            setIsSavingDutyDays(false);
        }
    }
    async function previewAutomatically() {
        setIsGenerating(true); setMessage(""); setIsError(false);
        try {
            const result = await previewAutoGeneratePlanning({ dateFrom: selectedRange.dateFrom, dateTo: selectedRange.dateTo });
            setLastGenerationResult(result);
            setMessage(`Podgląd zawiera ${result.generatedCount} przypisań i ${result.unassignedCount} nieobsadzonych służb. Żadne zmiany nie zostały zapisane.`);
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się przygotować podglądu planu."); }
        finally { setIsGenerating(false); }
    }

    async function generateAutomatically() {
        setIsGenerating(true); setMessage(""); setIsError(false);
        try {
            const result = await autoGeneratePlanning({ dateFrom: selectedRange.dateFrom, dateTo: selectedRange.dateTo });
            setLastGenerationResult(result);
            const loadedSchedules = await getSchedules(); setSchedules(loadedSchedules); await refreshMonth(selectedYear, selectedMonth, loadedSchedules);
            const weeklyRestWarnings = result.driverSummaries.reduce((sum, driver) => sum + driver.weeklyRestWarnings.length + driver.insufficientWeeklyRestCount, 0);
            setMessage(`Wygenerowano ${result.generatedCount} przypisań. Nieobsadzone służby: ${result.unassignedCount}. Odrzucone kandydatury: ${result.candidateRejectionCount}. Konflikty czasowe: ${result.timeConflictRejectionCount}. Odpoczynek dobowy: ${result.dailyRestRejectionCount}. Odpoczynek tygodniowy: ${result.weeklyRestRejectionCount}. Zakazy: ${result.forbiddenCandidateRejectionCount}. Ostrzeżenia tygodniowe: ${weeklyRestWarnings}.`);
        } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się wygenerować planu automatycznie."); }
        finally { setIsGenerating(false); }
    }
    async function checkScheduleValidation() { if (!selectedSchedule) return; setIsValidating(true); setMessage(""); setIsError(false); try { setValidation(await validateSchedule(selectedSchedule.id)); setMessage("Walidacja grafiku została wykonana."); } catch (error) { setIsError(true); setMessage(error instanceof Error ? error.message : "Nie udało się sprawdzić grafiku."); } finally { setIsValidating(false); } }

    useEffect(() => { void loadInitialData(); }, []);

    return (
        <section className="drivers-panel planning-schedules-panel">
            <div className="section-heading planning-panel-heading"><div><h3>Grafiki miesięczne</h3><p>Miesięczny raport kierowca × dzień z ręczną edycją komórek i automatycznym generowaniem.</p></div><button className="planning-primary-button" type="button" onClick={resetForm}>Nowy grafik</button></div>
            {message ? <p className={`drivers-message${isError ? " error" : " success"}`}>{message}</p> : null}
            <div className="planning-schedules-layout">
                <aside className="planning-schedules-list">
                    <h4>Lista grafików</h4>{isLoading ? <p className="drivers-status">Ładowanie grafików...</p> : null}{!isLoading && schedules.length === 0 ? <p className="drivers-status">Brak grafików.</p> : null}
                    {schedules.map((schedule) => <button key={schedule.id} className={`planning-schedule-item${selectedSchedule?.id === schedule.id ? " active" : ""}`} type="button" onClick={() => void selectSchedule(schedule.id)}><strong>{schedule.name}</strong><span>{schedule.month.toString().padStart(2, "0")}/{schedule.year} · {schedule.assignmentsCount} przypisań</span></button>)}
                </aside>
                <div className="planning-schedules-main">
                    <PlanningMonthNavigation year={selectedYear} month={selectedMonth} onPreviousMonth={() => void changeMonth(selectedYear, selectedMonth - 1)} onNextMonth={() => void changeMonth(selectedYear, selectedMonth + 1)} onCurrentMonth={() => void changeMonth(currentMonth.year, currentMonth.month)} onMonthChange={(year, month) => void changeMonth(year, month)} />
                    <form className="planning-schedule-form" onSubmit={(event) => void saveSchedule(event)}>
                        <label>Nazwa<input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required /></label><label>Rok<input type="number" min="2000" max="2100" value={form.year} onChange={(event) => setForm({ ...form, year: event.target.value })} required /></label><label>Miesiąc<input type="number" min="1" max="12" value={form.month} onChange={(event) => setForm({ ...form, month: event.target.value })} required /></label><label>Uwagi<input value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></label>
                        <div className="driver-row-actions"><button type="submit" className="planning-primary-button" disabled={isSaving}>{isSaving ? "Zapisywanie..." : "Zapisz grafik"}</button>{selectedSchedule ? <button type="button" className="driver-delete-button" onClick={() => void removeSchedule(selectedSchedule.id)}>Usuń grafik</button> : null}</div>
                    </form>
                    <div className="planning-month-primary"><div className="planning-validation-header"><div><h4>Plan miesięczny</h4><p>{selectedSchedule ? selectedSchedule.name : "Brak zapisanego grafiku dla tego miesiąca. Kliknij komórkę, aby utworzyć pierwszy wpis."}</p></div><div className="driver-row-actions"><button className="planning-secondary-button" type="button" onClick={() => void checkScheduleValidation()} disabled={isValidating || !selectedSchedule}>{isValidating ? "Sprawdzanie..." : "Sprawdź grafik"}</button><button className="planning-secondary-button" type="button" onClick={() => void previewAutomatically()} disabled={isGenerating}>{isGenerating ? "Przetwarzanie..." : "Podgląd generowania"}</button><button className="planning-primary-button" type="button" onClick={() => void generateAutomatically()} disabled={isGenerating}>{isGenerating ? "Przetwarzanie..." : "Generuj i zapisz"}</button></div></div><PlanningGenerationSummary result={lastGenerationResult} /><PlanningMonthlyGrid grid={grid} onCellClick={openEditor} /></div>
                    <div className="planning-compact-info">
                        <span>Do planowania wybrano <strong>{planningEnabledDriversCount}</strong> z <strong>{drivers.length}</strong> kierowców.</span>
                        <Link className="planning-secondary-button" to="/drivers">Zarządzaj w zakładce Kierowcy</Link>
                    </div>

                    <div className="planning-validation-panel planning-duty-days-panel">
                        <div className="planning-validation-header"><div><h4>Dni wykonywania służb</h4><p>Brak zapisanej konfiguracji oznacza domyślnie dni robocze, poniedziałek-piątek.</p></div></div>
                        <div className="planning-duty-days-list">
                            {duties.map((duty) => {
                                const mask = getDutyMask(duty);
                                const includeHolidays = duty.includeHolidays;
                                return <div key={duty.id} className="planning-duty-days-item"><strong>{duty.dutyNumber}</strong><span>{duty.name}</span><div className="planning-duty-day-buttons">{planningDutyDayOptions.map(([bit, label]) => <label key={bit}><input type="checkbox" checked={(mask & bit) !== 0} onChange={(event) => { const nextMask = togglePlanningDutyDay(mask, bit, event.target.checked); void saveDutyDays(duty, nextMask, includeHolidays); }} /> {label}</label>)}<label><input type="checkbox" checked={includeHolidays} onChange={(event) => void saveDutyDays(duty, mask, event.target.checked)} /> Święta</label></div><div className="driver-row-actions"><button type="button" className="planning-secondary-button" disabled={isSavingDutyDays} onClick={() => void saveDutyDays(duty, planningWeekdaysMask, false)}>Dni robocze</button><button type="button" className="planning-secondary-button" disabled={isSavingDutyDays} onClick={() => void saveDutyDays(duty, planningWeekendMask, false)}>Weekend</button><button type="button" className="planning-secondary-button" disabled={isSavingDutyDays} onClick={() => void saveDutyDays(duty, planningAllWeekMask, true)}>Cały tydzień</button><button type="button" className="planning-secondary-button" disabled={isSavingDutyDays} onClick={() => void saveDutyDays(duty, 0, false)}>Wyczyść</button></div></div>;
                            })}
                        </div>
                    </div>
                    <details className="planning-details-panel"><summary>Widok listy i narzędzia planowania</summary>
                        <div className="planning-validation-panel"><div className="planning-validation-header"><div><h4>Widok listy</h4><p>Szczegółowa lista przypisań dla wybranego miesiąca.</p></div><button className="planning-secondary-button" type="button" onClick={() => void refreshMonth()} disabled={isGenerating}>Odśwież dane</button></div><div className="drivers-table-wrapper"><table className="drivers-table planning-table"><thead><tr><th>Data</th><th>Kierowca</th><th>Nr służby</th><th>Start</th><th>Koniec</th><th>Status</th></tr></thead><tbody>{autoAssignments.length === 0 ? <tr><td colSpan={6}>Brak przypisań w wybranym miesiącu.</td></tr> : autoAssignments.map((assignment) => <tr key={assignment.id}><td>{assignment.workDate}</td><td>{assignment.driverFullName}</td><td>{assignment.dutyNumber ?? "-"}</td><td>{formatDateTime(assignment.startDateTime)}</td><td>{formatDateTime(assignment.endDateTime)}</td><td>{formatStatus(assignment.status)}</td></tr>)}</tbody></table></div></div>
                        <div className="planning-validation-panel"><div className="planning-validation-header"><div><h4>Walidacja grafiku</h4><p>Sprawdź podstawowe konflikty i braki w grafiku miesięcznym.</p></div><button className="planning-secondary-button" type="button" onClick={() => void checkScheduleValidation()} disabled={isValidating || !selectedSchedule}>{isValidating ? "Sprawdzanie..." : "Sprawdź grafik"}</button></div>{validation ? <div className="planning-validation-results"><div className="planning-validation-summary"><span><strong>{validation.errorCount}</strong> błędów</span><span><strong>{validation.warningCount}</strong> ostrzeżeń</span></div>{validation.warnings.length === 0 ? <p className="drivers-status">Nie znaleziono problemów w grafiku.</p> : <ul className="planning-validation-list">{validation.warnings.map((warning, index) => <li key={`${warning.assignmentId ?? warning.code}-${index}`} className={`planning-validation-item ${warning.severity.toLowerCase()}`}><span className="planning-validation-severity">{warning.severity === "Error" ? "Błąd" : warning.severity === "Warning" ? "Ostrzeżenie" : "Info"}</span><span>{warning.date ?? "Brak daty"}</span><span>{warning.driverName ?? "Brak kierowcy"}</span><span>{warning.code}</span><strong>{warning.message}</strong></li>)}</ul>}</div> : null}</div>
                        <div className="planning-validation-panel"><div className="planning-validation-header"><div><h4>Ograniczenia i preferencje</h4><p>Zakazy blokują przydział kierowcy do służby, preferencje wzmacniają wybór.</p></div><button className="planning-secondary-button" type="button" onClick={() => void loadAssignmentRules()} disabled={isSavingRule}>Odśwież reguły</button></div><div className="planning-schedule-form"><label>Kierowca<select value={ruleForm.driverId} onChange={(event) => setRuleForm({ ...ruleForm, driverId: event.target.value })}><option value="">Wybierz kierowcę</option>{drivers.map((driver) => <option key={driver.id} value={driver.id}>{formatDriverLastFirst(driver)}</option>)}</select></label><label>Służba<select value={ruleForm.dutyId} onChange={(event) => setRuleForm({ ...ruleForm, dutyId: event.target.value })}><option value="">Wybierz służbę</option>{duties.map((duty) => <option key={duty.id} value={duty.id}>{duty.dutyNumber} · {duty.name}</option>)}</select></label><label>Typ<select value={ruleForm.type} onChange={(event) => setRuleForm({ ...ruleForm, type: event.target.value as RuleForm["type"] })}><option value="Forbidden">Zakaz</option><option value="Preferred">Preferencja</option></select></label><label>Ważne od<input type="date" value={ruleForm.validFrom} onChange={(event) => setRuleForm({ ...ruleForm, validFrom: event.target.value })} /></label><label>Ważne do<input type="date" value={ruleForm.validTo} onChange={(event) => setRuleForm({ ...ruleForm, validTo: event.target.value })} /></label><label>Notatka<input value={ruleForm.notes} onChange={(event) => setRuleForm({ ...ruleForm, notes: event.target.value })} /></label><div className="driver-row-actions"><button className="planning-primary-button" type="button" onClick={() => void saveAssignmentRule()} disabled={isSavingRule || !ruleForm.driverId || !ruleForm.dutyId}>{isSavingRule ? "Zapisywanie..." : "Dodaj regułę"}</button></div></div>{assignmentRules.length === 0 ? <p className="drivers-status">Brak zapisanych zakazów i preferencji.</p> : <div className="planning-availability-list">{assignmentRules.map((rule) => <div key={rule.id} className="planning-availability-item"><strong>{rule.driverFullName} · {rule.dutyNumber}</strong><span>{rule.type === "Forbidden" ? "Zakaz" : "Preferencja"}{rule.validFrom || rule.validTo ? ` · ${rule.validFrom ?? "..."} - ${rule.validTo ?? "..."}` : ""}</span>{rule.notes ? <small>{rule.notes}</small> : null}<button className="driver-delete-button" type="button" onClick={() => void removeAssignmentRule(rule.id)} disabled={isSavingRule}>Usuń</button></div>)}</div>}</div>
                        <div className="planning-validation-panel"><div className="planning-validation-header"><div><h4>Dostępność kierowców</h4><p>Wpisy blokują kierowcę przy automatycznym planowaniu.</p></div><button className="planning-secondary-button" type="button" onClick={() => void loadDriverAvailability()} disabled={isSavingAvailability}>Odśwież dostępność</button></div><div className="planning-schedule-form"><label>Kierowca<select value={availabilityForm.driverId} onChange={(event) => setAvailabilityForm({ ...availabilityForm, driverId: event.target.value })}><option value="">Wybierz kierowcę</option>{drivers.map((driver) => <option key={driver.id} value={driver.id}>{formatDriverLastFirst(driver)}</option>)}</select></label><label>Data od<input type="date" value={availabilityForm.dateFrom} onChange={(event) => setAvailabilityForm({ ...availabilityForm, dateFrom: event.target.value })} /></label><label>Data do<input type="date" value={availabilityForm.dateTo} onChange={(event) => setAvailabilityForm({ ...availabilityForm, dateTo: event.target.value })} /></label><label>Typ<select value={availabilityForm.type} onChange={(event) => setAvailabilityForm({ ...availabilityForm, type: event.target.value as PlanningDriverAvailabilityType })}>{availabilityTypes.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label><label>Notatka<input value={availabilityForm.note} onChange={(event) => setAvailabilityForm({ ...availabilityForm, note: event.target.value })} /></label><div className="driver-row-actions"><button className="planning-primary-button" type="button" onClick={() => void saveDriverAvailability()} disabled={isSavingAvailability || !availabilityForm.driverId}>{isSavingAvailability ? "Zapisywanie..." : "Dodaj dostępność"}</button></div></div><div className="drivers-table-wrapper"><table className="drivers-table planning-table"><thead><tr><th>Kierowca</th><th>Od</th><th>Do</th><th>Typ</th><th>Notatka</th><th>Akcje</th></tr></thead><tbody>{driverAvailability.length === 0 ? <tr><td colSpan={6}>Brak wpisów dostępności w wybranym miesiącu.</td></tr> : driverAvailability.map((item) => <tr key={item.id}><td>{item.driverFullName}</td><td>{item.dateFrom}</td><td>{item.dateTo}</td><td>{availabilityTypeLabels[item.type] ?? item.type}</td><td>{item.note ?? "-"}</td><td><button className="driver-delete-button" type="button" onClick={() => void removeDriverAvailability(item.id)} disabled={isSavingAvailability}>Usuń</button></td></tr>)}</tbody></table></div></div>
                    </details>
                </div>
            </div>
            {editor ? <PlanningAssignmentEditorModal editor={editor} driver={selectedDriver} assignment={selectedAssignment} duties={duties} isSaving={isSaving} onChange={setEditor} onSave={() => void saveAssignment()} onDelete={() => void removeAssignment()} onClose={() => setEditor(null)} /> : null}
        </section>
    );
}











