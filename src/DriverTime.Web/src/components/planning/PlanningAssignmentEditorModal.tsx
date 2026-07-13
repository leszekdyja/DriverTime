import type { Driver } from "../../services/driversService";
import type { PlanningDuty } from "../../services/planningDutiesService";
import type { PlanningAssignment } from "../../services/planningSchedulesService";
import { formatDriverLastFirst } from "./buildPlanningMonthlyGrid";

export const planningManualEntryTypes = [
    ["Duty", "Zwykła służba"],
    ["RN", "RN"],
    ["R", "R"],
    ["R2", "R2"],
    ["WG", "WG"],
    ["W", "W"],
    ["Vacation", "Urlop"],
    ["SickLeave", "Chorobowe"],
    ["Unavailable", "Niedostępność"],
] as const;

export type PlanningManualEntryCode = typeof planningManualEntryTypes[number][0];

export type PlanningAssignmentEditorState = {
    driverId: string;
    date: string;
    assignmentId: string | null;
    entryCode: PlanningManualEntryCode;
    planningDutyId: string;
    notes: string;
};

type PlanningAssignmentEditorModalProps = {
    editor: PlanningAssignmentEditorState;
    driver: Driver | null;
    assignment: PlanningAssignment | null;
    duties: PlanningDuty[];
    isSaving: boolean;
    onChange: (editor: PlanningAssignmentEditorState) => void;
    onSave: () => void;
    onDelete: () => void;
    onClose: () => void;
};

export function PlanningAssignmentEditorModal({
    editor,
    driver,
    assignment,
    duties,
    isSaving,
    onChange,
    onSave,
    onDelete,
    onClose,
}: PlanningAssignmentEditorModalProps) {
    return (
        <div className="planning-editor-backdrop" role="presentation">
            <div className="planning-assignment-editor" role="dialog" aria-modal="true" aria-label="Edycja wpisu grafiku">
                <div className="planning-editor-title">
                    <div>
                        <h4>{driver ? formatDriverLastFirst(driver) : "Kierowca"}</h4>
                        <p>{editor.date}</p>
                    </div>
                    <button className="planning-icon-button" type="button" onClick={onClose} aria-label="Zamknij">×</button>
                </div>

                <div className="planning-current-entry">
                    <span>Obecny wpis</span>
                    <strong>{assignment ? assignment.dutyNumber || assignment.assignmentType : "Brak"}</strong>
                </div>

                <label>Typ wpisu
                    <select value={editor.entryCode} onChange={(event) => onChange({ ...editor, entryCode: event.target.value as PlanningManualEntryCode })}>
                        {planningManualEntryTypes.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
                    </select>
                </label>

                {editor.entryCode === "Duty" ? (
                    <label>Służba
                        <select value={editor.planningDutyId} onChange={(event) => onChange({ ...editor, planningDutyId: event.target.value })}>
                            <option value="">Wybierz służbę</option>
                            {duties.map((duty) => <option key={duty.id} value={duty.id}>{duty.dutyNumber} · {duty.name}</option>)}
                        </select>
                    </label>
                ) : null}

                <label>Notatka
                    <textarea value={editor.notes} onChange={(event) => onChange({ ...editor, notes: event.target.value })} />
                </label>

                <div className="driver-row-actions">
                    <button className="planning-primary-button" type="button" onClick={onSave} disabled={isSaving || (editor.entryCode === "Duty" && !editor.planningDutyId)}>
                        {isSaving ? "Zapisywanie..." : "Zapisz"}
                    </button>
                    {editor.assignmentId ? <button className="driver-delete-button" type="button" onClick={onDelete} disabled={isSaving}>Usuń</button> : null}
                    <button className="planning-secondary-button" type="button" onClick={onClose} disabled={isSaving}>Anuluj</button>
                </div>
            </div>
        </div>
    );
}
