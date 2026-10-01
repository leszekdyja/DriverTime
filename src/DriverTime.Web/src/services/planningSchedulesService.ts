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
    minDailyRestMinutes?: number | null;
    maxConsecutiveWorkDays?: number | null;
    maxWeeklyWorkMinutes?: number | null;
    targetMonthlyWorkMinutes?: number | null;
    calculateMonthlyTargetFromCalendar?: boolean | null;
    enforceMonthlyTargetMaximum?: boolean | null;
    enforceWeeklyMaximum?: boolean | null;
    includeManualAssignmentsInWorkload?: boolean | null;
    includeAssignmentsOutsideGeneratedRangeForRestChecks?: boolean | null;
    minWeeklyRestMinutes?: number | null;
    regularWeeklyRestMinutes?: number | null;
    preferredWeeklyRestMinutes?: number | null;
    assignmentRules?: PlanningAssignmentRule[];
};


export type PlanningAssignmentRule = {
    companyId?: string | null;
    driverId: string;
    dutyId?: string | null;
    dutyNumber?: string | null;
    type: "Forbidden" | "Preferred" | string;
    dateFrom?: string | null;
    dateTo?: string | null;
    note?: string | null;
};
export type PlanningCandidateScoreBreakdown = {
    assignmentCountScore: number;
    workMinutesScore: number;
    monthlyDeficitScore: number;
    calendarTargetDeficitScore: number;
    weeklyLoadScore: number;
    consecutiveDaysScore: number;
    weeklyRestScore: number;
    reducedWeeklyRestPenalty: number;
    preferenceScore: number;
    totalScore: number;
};

export type PlanningCandidateEvaluation = {
    driverId: string;
    driverName: string;
    dutyId: string;
    date: string;
    isEligible: boolean;
    score: number;
    scoreBreakdown: PlanningCandidateScoreBreakdown;
    rejectionReasons: string[];
    rejectionReasonDescriptions: string[];
    previousAssignmentEnd: string | null;
    nextAssignmentStart: string | null;
    restBeforeMinutes: number | null;
    restAfterMinutes: number | null;
    weeklyWorkMinutesBefore: number;
    weeklyWorkMinutesAfter: number;
    weeklyWorkMinutesLimit: number;
    monthlyWorkMinutesBefore: number;
    monthlyWorkMinutesAfter: number;
    realWorkMinutesBefore: number;
    realWorkMinutesAfter: number;
    creditedAbsenceMinutes: number;
    monthlyNormMinutesBefore: number;
    monthlyNormMinutesAfter: number;
    targetMonthlyWorkMinutes: number | null;
    monthlyWorkMinutesDeficit: number | null;
    consecutiveWorkDaysBefore: number;
    consecutiveWorkDaysAfter: number;
    maxConsecutiveWorkDays: number;
    weeklyRestMinutesBeforeCandidate: number | null;
    weeklyRestMinutesAfterCandidate: number | null;
    weeklyRestKind: string;
    weeklyRestWarnings: string[];
    matchesPreference: boolean;
    preferenceScore: number;
    constraintMatches: string[];
    warnings: string[];
};

export type PlanningUnassignedDuty = {
    dutyId: string;
    dutyNumber: string;
    date: string;
    startDateTime: string | null;
    endDateTime: string | null;
    candidateEvaluations: PlanningCandidateEvaluation[];
    summary: string;
};

export type PlanningDriverGenerationSummary = {
    driverId: string;
    driverName: string;
    existingManualAssignments: number;
    generatedAssignments: number;
    totalAssignments: number;
    workMinutesBefore: number;
    generatedWorkMinutes: number;
    workMinutesAfter: number;
    realWorkMinutesBefore: number;
    creditedAbsenceMinutesBefore: number;
    realWorkMinutesGenerated: number;
    creditedAbsenceMinutesGenerated: number;
    realWorkMinutesAfter: number;
    creditedAbsenceMinutesAfter: number;
    monthlyNormMinutesAfter: number;
    targetMonthlyWorkMinutes: number | null;
    targetMonthlyWorkMinutesSource: string;
    calendarTargetWorkMinutes: number | null;
    monthlyDeficitAfter: number | null;
    maxWeeklyWorkMinutesObserved: number;
    maxConsecutiveWorkDaysObserved: number;
    reducedWeeklyRestCount: number;
    regularWeeklyRestCount: number;
    insufficientWeeklyRestCount: number;
    weeklyRestWarnings: string[];
    warnings: string[];
};

export type PlanningPublicHoliday = {
    date: string;
    name: string;
};

export type PlanningMonthlyWorkingTimeCalendar = {
    year: number;
    month: number;
    weekdayCount: number;
    holidayReductionDays: number;
    workingDays: number;
    targetWorkMinutes: number;
    holidays: PlanningPublicHoliday[];
    standardWorkingDays: string[];
};

export type PlanningAutoGenerateResult = {
    dateFrom: string;
    dateTo: string;
    isPreview: boolean;
    proposedAssignments: PlanningAssignmentListItem[];
    generatedCount: number;
    conflictCount: number;
    candidateRejectionCount: number;
    timeConflictRejectionCount: number;
    dailyRestRejectionCount: number;
    weeklyRestRejectionCount: number;
    manualAssignmentsPreserved: number;
    nightDutyGeneratedCount: number;
    rescueResolvedDutyCount: number;
    directSwapCount: number;
    chainSwapCount: number;
    removedWeeklyDayOffCount: number;
    removedDayOffCount: number;
    removedReserveFirstShiftCount: number;
    removedReserveSecondShiftCount: number;
    reserveGeneratedCount: number;
    dayOffGeneratedCount: number;
    forbiddenCandidateRejectionCount: number;
    preferredAssignmentCount: number;
    constraintBlockedUnassignedCount: number;
    vehicleDataWarningCount: number;
    unassignedCount: number;
    unassignedDuties: PlanningUnassignedDuty[];
    driverSummaries: PlanningDriverGenerationSummary[];
    monthlyCalendars: PlanningMonthlyWorkingTimeCalendar[];
    warnings: string[];
    messages: string[];
    timings: Array<{ stage: string; elapsedMilliseconds: number }>;
};
export type PlanningDriverDutyRule = {
    id: string;
    driverId: string;
    driverFullName: string;
    dutyId: string;
    dutyNumber: string;
    dutyName: string;
    type: "Forbidden" | "Preferred" | string;
    validFrom: string | null;
    validTo: string | null;
    notes: string | null;
    createdAtUtc: string;
    updatedAtUtc: string | null;
};

export type PlanningDriverDutyRulePayload = {
    driverId: string;
    dutyId: string;
    type: "Forbidden" | "Preferred" | string;
    validFrom?: string | null;
    validTo?: string | null;
    notes?: string | null;
};

export type PlanningManualAssignmentPayload = {
    driverId: string;
    date: string;
    dutyId?: string | null;
    entryCode?: string | null;
    notes?: string | null;
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


export type PlanningDriverAvailabilityType = "Available" | "Vacation" | "DayOff" | "SickLeave" | "Unavailable";

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
        if (response.status === 504) {
            throw new Error("Generator przekroczył limit czasu odpowiedzi serwera. Spróbuj ponownie po zawężeniu zakresu albo sprawdź logi backendu z czasami etapów generowania.");
        }

        let message = fallbackMessage;
        const text = await response.text().catch(() => "");
        if (text) {
            try {
                const body = JSON.parse(text);
                if (Array.isArray(body?.errors) && body.errors.length > 0) {
                    message = body.errors.join(" ");
                } else if (typeof body?.detail === "string" && body.detail.trim()) {
                    message = body.detail;
                } else if (typeof body?.message === "string" && body.message.trim()) {
                    message = body.message;
                } else if (typeof body?.title === "string" && body.title.trim()) {
                    message = body.title;
                }
            } catch {
                message = text;
            }
        }

        throw new Error(message);
    }

    return response.json() as Promise<T>;
}

export async function getPlanningAssignmentRules(filters?: { driverId?: string; dutyId?: string; type?: string; activeOn?: string }): Promise<PlanningDriverDutyRule[]> {
    const params = new URLSearchParams();
    if (filters?.driverId) params.set("driverId", filters.driverId);
    if (filters?.dutyId) params.set("dutyId", filters.dutyId);
    if (filters?.type) params.set("type", filters.type);
    if (filters?.activeOn) params.set("activeOn", filters.activeOn);
    const query = params.toString();
    const response = await apiFetch(`/api/planning/assignment-rules${query ? `?${query}` : ""}`);
    return readJson<PlanningDriverDutyRule[]>(response, "Nie udało się pobrać reguł planowania.");
}

export async function createPlanningAssignmentRule(payload: PlanningDriverDutyRulePayload): Promise<PlanningDriverDutyRule> {
    const response = await apiFetch("/api/planning/assignment-rules", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningDriverDutyRule>(response, "Nie udało się zapisać reguły planowania.");
}

export async function updatePlanningAssignmentRule(id: string, payload: PlanningDriverDutyRulePayload): Promise<PlanningDriverDutyRule> {
    const response = await apiFetch(`/api/planning/assignment-rules/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningDriverDutyRule>(response, "Nie udało się zaktualizować reguły planowania.");
}

export async function deletePlanningAssignmentRule(id: string): Promise<void> {
    const response = await apiFetch(`/api/planning/assignment-rules/${id}`, { method: "DELETE" });
    if (!response.ok) {
        throw new Error(response.status === 404 ? "Nie znaleziono reguły w Twojej firmie." : "Nie udało się usunąć reguły planowania.");
    }
}

export async function createManualPlanningAssignment(payload: PlanningManualAssignmentPayload): Promise<PlanningAssignment> {
    const response = await apiFetch("/api/planning/assignments/manual", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningAssignment>(response, "Nie udało się zapisać ręcznego przydziału.");
}

export async function updatePlanningAssignment(id: string, payload: PlanningManualAssignmentPayload): Promise<PlanningAssignment> {
    const response = await apiFetch(`/api/planning/assignments/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningAssignment>(response, "Nie udało się zaktualizować przydziału.");
}

export async function deletePlanningAssignment(id: string): Promise<void> {
    const response = await apiFetch(`/api/planning/assignments/${id}`, { method: "DELETE" });
    if (!response.ok) {
        throw new Error(response.status === 404 ? "Nie znaleziono przydziału w Twojej firmie." : "Nie udało się usunąć przydziału.");
    }
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

export async function previewAutoGeneratePlanning(payload: PlanningAutoGeneratePayload): Promise<PlanningAutoGenerateResult> {
    const response = await apiFetch("/api/planning/auto-generate/preview", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    return readJson<PlanningAutoGenerateResult>(response, "Nie udało się przygotować podglądu planu.");
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
