import { apiFetch } from "./apiClient";

export type PlanningAssignmentType = "Duty" | "DayOff" | "Vacation" | "SickLeave" | "Training" | "Other";

export type PlanningScheduleListItem = {
    id: string;
    name: string;
    year: number;
    month: number;
    notes: string | null;
    createdUtc: string;
    updatedUtc: string | null;
    assignmentsCount: number;
};

export type PlanningAssignment = {
    id: string;
    date: string;
    driverId: string;
    driverFullName: string;
    planningDutyId: string | null;
    dutyNumber: string | null;
    line: string | null;
    startTime: string | null;
    endTime: string | null;
    assignmentType: PlanningAssignmentType;
    notes: string | null;
};

export type PlanningSchedule = PlanningScheduleListItem & {
    assignments: PlanningAssignment[];
};

export type PlanningSchedulePayload = {
    name: string;
    year: number;
    month: number;
    notes?: string | null;
};

export type PlanningAssignmentPayload = {
    date: string;
    driverId: string;
    planningDutyId?: string | null;
    assignmentType: PlanningAssignmentType;
    notes?: string | null;
};

export type PlanningScheduleValidationWarning = {
    severity: "Info" | "Warning" | "Error" | string;
    date: string | null;
    driverId: string | null;
    driverName: string | null;
    assignmentId: string | null;
    code: string;
    message: string;
};

export type PlanningScheduleValidation = {
    scheduleId: string;
    warningCount: number;
    errorCount: number;
    warnings: PlanningScheduleValidationWarning[];
};

async function readJson<T>(response: Response, fallbackMessage: string): Promise<T> {
    if (!response.ok) {
        let message = fallbackMessage;
        try {
            const body = await response.json();
            if (Array.isArray(body?.errors) && body.errors.length > 0) {
                message = body.errors.join(" ");
            }
        } catch {
            // Keep fallback message.
        }
        throw new Error(message);
    }

    return response.json() as Promise<T>;
}

export async function getSchedules(): Promise<PlanningScheduleListItem[]> {
    const response = await apiFetch("/api/planning/schedules");
    return readJson<PlanningScheduleListItem[]>(response, "Nie udało się pobrać grafików.");
}

export async function getSchedule(id: string): Promise<PlanningSchedule> {
    const response = await apiFetch(`/api/planning/schedules/${id}`);
    return readJson<PlanningSchedule>(response, "Nie udało się pobrać grafiku.");
}

export async function validateSchedule(id: string): Promise<PlanningScheduleValidation> {
    const response = await apiFetch(`/api/planning/schedules/${id}/validation`);
    return readJson<PlanningScheduleValidation>(response, "Nie udało się sprawdzić grafiku.");
}

export async function createSchedule(payload: PlanningSchedulePayload): Promise<PlanningSchedule> {
    const response = await apiFetch("/api/planning/schedules", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningSchedule>(response, "Nie udało się utworzyć grafiku.");
}

export async function updateSchedule(id: string, payload: PlanningSchedulePayload): Promise<PlanningSchedule> {
    const response = await apiFetch(`/api/planning/schedules/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningSchedule>(response, "Nie udało się zapisać grafiku.");
}

export async function deleteSchedule(id: string): Promise<void> {
    const response = await apiFetch(`/api/planning/schedules/${id}`, { method: "DELETE" });
    if (!response.ok) {
        throw new Error(response.status === 404 ? "Nie znaleziono grafiku w Twojej firmie." : "Nie udało się usunąć grafiku.");
    }
}

export async function upsertAssignment(scheduleId: string, payload: PlanningAssignmentPayload): Promise<PlanningAssignment> {
    const response = await apiFetch(`/api/planning/schedules/${scheduleId}/assignments`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningAssignment>(response, "Nie udało się zapisać przypisania.");
}

export async function deleteAssignment(scheduleId: string, assignmentId: string): Promise<void> {
    const response = await apiFetch(`/api/planning/schedules/${scheduleId}/assignments/${assignmentId}`, { method: "DELETE" });
    if (!response.ok) {
        throw new Error(response.status === 404 ? "Nie znaleziono przypisania w tym grafiku." : "Nie udało się usunąć przypisania.");
    }
}

