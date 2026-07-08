import { apiFetch } from "./apiClient";

export type PlanningAssignmentType = "Duty" | "DayOff" | "Vacation" | "SickLeave" | "Training" | "Other";
export type PlanningAssignmentStatus = "Generated" | "Manual" | "Conflict" | string;

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
    startDateTime: string | null;
    endDateTime: string | null;
    status: PlanningAssignmentStatus;
    assignmentType: PlanningAssignmentType;
    notes: string | null;
};

export type PlanningAutoGeneratePayload = {
    dateFrom: string;
    dateTo: string;
    driverIds?: string[];
};

export type PlanningAutoGenerateResult = {
    dateFrom: string;
    dateTo: string;
    generatedCount: number;
    conflictCount: number;
    manualAssignmentsPreserved: number;
    messages: string[];
};

export type PlanningAssignmentListItem = {
    id: string;
    workDate: string;
    driverId: string;
    driverFullName: string;
    planningDutyId: string | null;
    dutyNumber: string | null;
    startDateTime: string | null;
    endDateTime: string | null;
    status: PlanningAssignmentStatus;
};


export type PlanningDriverAvailabilityType = "Vacation" | "DayOff" | "SickLeave" | "Unavailable";

export type PlanningDriverAvailability = {
    id: string;
    driverId: string;
    driverFullName: string;
    dateFrom: string;
    dateTo: string;
    type: PlanningDriverAvailabilityType;
    note: string | null;
    createdAtUtc: string;
};

export type PlanningDriverAvailabilityPayload = {
    driverId: string;
    dateFrom: string;
    dateTo: string;
    type: PlanningDriverAvailabilityType;
    note?: string | null;
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

export async function autoGeneratePlanning(payload: PlanningAutoGeneratePayload): Promise<PlanningAutoGenerateResult> {
    const response = await apiFetch("/api/planning/auto-generate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningAutoGenerateResult>(response, "Nie udało się wygenerować planu automatycznie.");
}

export async function getPlanningAssignments(dateFrom: string, dateTo: string): Promise<PlanningAssignmentListItem[]> {
    const params = new URLSearchParams({ dateFrom, dateTo });
    const response = await apiFetch(`/api/planning/assignments?${params.toString()}`);
    return readJson<PlanningAssignmentListItem[]>(response, "Nie udało się pobrać przypisań planu.");
}

export async function getPlanningDriverAvailability(dateFrom: string, dateTo: string): Promise<PlanningDriverAvailability[]> {
    const params = new URLSearchParams({ dateFrom, dateTo });
    const response = await apiFetch(`/api/planning/driver-availability?${params.toString()}`);
    return readJson<PlanningDriverAvailability[]>(response, "Nie udało się pobrać dostępności kierowców.");
}

export async function createPlanningDriverAvailability(payload: PlanningDriverAvailabilityPayload): Promise<PlanningDriverAvailability> {
    const response = await apiFetch("/api/planning/driver-availability", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningDriverAvailability>(response, "Nie udało się dodać dostępności kierowcy.");
}

export async function deletePlanningDriverAvailability(id: string): Promise<void> {
    const response = await apiFetch(`/api/planning/driver-availability/${id}`, { method: "DELETE" });
    if (!response.ok) {
        throw new Error(response.status === 404 ? "Nie znaleziono wpisu dostępności." : "Nie udało się usunąć dostępności kierowcy.");
    }
}
