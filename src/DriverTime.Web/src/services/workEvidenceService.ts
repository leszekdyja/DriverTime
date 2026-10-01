import { apiFetch } from "./apiClient";

export type WorkEvidenceActivityType =
    | "Driving"
    | "OtherWork"
    | "Availability"
    | "BreakRest"
    | "Vacation"
    | "SickLeave"
    | "DayOff"
    | "OtherAbsence";

export type WorkEvidenceEntry = {
    id: string;
    date: string;
    startDateTime: string;
    endDateTime: string;
    startTime: string;
    endTime: string;
    endsNextDay: boolean;
    activityType: WorkEvidenceActivityType;
    source: "Manual" | "Ddd" | "Correction";
    vehicleRegistration: string | null;
    countryCode: string | null;
    distanceKm: number | null;
    description: string | null;
    durationMinutes: number;
};

export type WorkEvidenceDay = {
    date: string;
    drivingMinutes: number;
    otherWorkMinutes: number;
    availabilityMinutes: number;
    breakRestMinutes: number;
    absenceMinutes: number;
    totalTrackedMinutes: number;
    entries: WorkEvidenceEntry[];
};

export type WorkEvidenceSummary = {
    drivingMinutes: number;
    otherWorkMinutes: number;
    availabilityMinutes: number;
    breakRestMinutes: number;
    absenceMinutes: number;
    totalTrackedMinutes: number;
};

export type WorkEvidenceMonth = {
    driverId: string;
    driverFullName: string;
    year: number;
    month: number;
    summary: WorkEvidenceSummary;
    days: WorkEvidenceDay[];
};

export type WorkEvidenceEntryRequest = {
    date: string;
    startTime: string;
    endTime: string;
    endsNextDay: boolean;
    activityType: WorkEvidenceActivityType;
    vehicleRegistration?: string | null;
    countryCode?: string | null;
    distanceKm?: number | null;
    description?: string | null;
};

export async function getDriverWorkEvidence(
    driverId: string,
    year: number,
    month: number,
): Promise<WorkEvidenceMonth> {
    const response = await apiFetch(`/api/drivers/${driverId}/work-evidence?year=${year}&month=${month}`);

    if (!response.ok) {
        throw new Error(await readApiError(response, "Nie udało się pobrać ewidencji czasu kierowcy."));
    }

    return response.json() as Promise<WorkEvidenceMonth>;
}

export async function createWorkEvidenceEntry(
    driverId: string,
    request: WorkEvidenceEntryRequest,
): Promise<WorkEvidenceEntry> {
    const response = await apiFetch(`/api/drivers/${driverId}/work-evidence/entries`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, "Nie udało się zapisać wpisu ewidencji."));
    }

    return response.json() as Promise<WorkEvidenceEntry>;
}

export async function updateWorkEvidenceEntry(
    entryId: string,
    request: WorkEvidenceEntryRequest,
): Promise<WorkEvidenceEntry> {
    const response = await apiFetch(`/api/work-evidence/entries/${entryId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, "Nie udało się zaktualizować wpisu ewidencji."));
    }

    return response.json() as Promise<WorkEvidenceEntry>;
}

export async function deleteWorkEvidenceEntry(entryId: string): Promise<void> {
    const response = await apiFetch(`/api/work-evidence/entries/${entryId}`, {
        method: "DELETE",
    });

    if (!response.ok) {
        throw new Error(await readApiError(response, "Nie udało się usunąć wpisu ewidencji."));
    }
}

async function readApiError(response: Response, fallback: string) {
    try {
        const body = await response.json();

        if (Array.isArray(body?.errors)) {
            return body.errors.join(" ");
        }

        if (body?.errors && typeof body.errors === "object") {
            return Object.values(body.errors)
                .flatMap((value) => Array.isArray(value) ? value : [String(value)])
                .join(" ");
        }

        if (typeof body?.message === "string") {
            return body.message;
        }

        if (typeof body?.title === "string") {
            return body.title;
        }
    } catch {
        return fallback;
    }

    return fallback;
}
