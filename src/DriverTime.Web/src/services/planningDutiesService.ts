import { apiFetch } from "./apiClient";

export type PlanningDuty = {
    id: string;
    dutyNumber: string;
    name: string;
    validFrom: string | null;
    vehicleRequirement: string | null;
    startTime: string | null;
    endTime: string | null;
    totalDurationMinutes: number | null;
    workMinutes: number | null;
    breakMinutes: number | null;
    drivingMinutes: number | null;
    distanceKm: number | null;
    createdAtUtc: string;
    updatedAtUtc: string | null;
};

export type PlanningDutyLine = {
    id: string;
    lineCode: string;
    variant: string | null;
    distanceKm: number | null;
};

export type PlanningDutyStop = {
    id: string;
    sequence: number;
    stopName: string;
    km: number | null;
    tripGroup: string | null;
    arrivalTime: string | null;
    departureTime: string | null;
    lineCode: string | null;
};

export type PlanningDutyDetails = PlanningDuty & {
    notes: string | null;
    sourceFileName: string | null;
    lines: PlanningDutyLine[];
    stops: PlanningDutyStop[];
};

export type PlanningDutyPdfImportConfidence = {
    dutyNumber: number;
    startTime: number;
    endTime: number;
    line: number;
    stops: number;
    workingMinutes: number;
    drivingMinutes: number;
    breakMinutes: number;
    distanceKm: number;
};
export type PlanningDutyPdfImportPreviewItem = {
    dutyNumber: string | null;
    name: string | null;
    validFrom: string | null;
    vehicleRequirement: string | null;
    startTime: string | null;
    endTime: string | null;
    totalDurationMinutes: number | null;
    workMinutes: number | null;
    breakMinutes: number | null;
    drivingMinutes: number | null;
    distanceKm: number | null;
    notes: string | null;
    sourceFileName: string | null;
    lines: PlanningDutyLine[];
    stops: PlanningDutyStop[];
    confidence: PlanningDutyPdfImportConfidence;
};

export type PlanningDutyPdfImportPreview = {
    fileName: string;
    fileSizeBytes: number;
    detectedDutyCount: number;
    warnings: string[];
    duties: PlanningDutyPdfImportPreviewItem[];
};


export type PlanningDutyPdfImportConfirmStop = {
    stopName: string | null;
    arrivalTime: string | null;
    departureTime: string | null;
    sequence: number;
};

export type PlanningDutyPdfImportConfirmItem = {
    dutyNumber: string;
    dutyName: string | null;
    line: string | null;
    startTime: string | null;
    endTime: string | null;
    workingMinutes: number | null;
    drivingMinutes: number | null;
    breakMinutes: number | null;
    distanceKm: number | null;
    notes: string | null;
    stops: PlanningDutyPdfImportConfirmStop[];
};

export type PlanningDutyPdfImportConfirmRequest = {
    sourceFileName: string | null;
    duties: PlanningDutyPdfImportConfirmItem[];
};

export type PlanningDutyPdfImportConfirmResultItem = {
    dutyNumber: string | null;
    line: string | null;
    status: "Created" | "Updated" | "Unchanged" | "Skipped" | "Error" | string;
    message: string;
};

export type PlanningDutyPdfImportConfirmResult = {
    createdCount: number;
    updatedCount: number;
    unchangedCount: number;
    skippedCount: number;
    errors: string[];
    items: PlanningDutyPdfImportConfirmResultItem[];
};
export type PlanningDutyPayload = {
    dutyNumber: string;
    name: string;
    validFrom?: string | null;
    vehicleRequirement?: string | null;
    startTime?: string | null;
    endTime?: string | null;
    totalDurationMinutes?: number | null;
    workMinutes?: number | null;
    breakMinutes?: number | null;
    drivingMinutes?: number | null;
    distanceKm?: number | null;
    notes?: string | null;
    sourceFileName?: string | null;
    lines?: PlanningDutyLine[];
    stops?: PlanningDutyStop[];
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
            // Keep fallback message when API returned no JSON body.
        }

        throw new Error(message);
    }

    return response.json() as Promise<T>;
}

export async function getPlanningDuties(): Promise<PlanningDuty[]> {
    const response = await apiFetch("/api/planning/duties");

    return readJson<PlanningDuty[]>(response, "Nie udało się pobrać służb planowania.");
}

export async function getPlanningDuty(id: string): Promise<PlanningDutyDetails> {
    const response = await apiFetch(`/api/planning/duties/${id}`);

    return readJson<PlanningDutyDetails>(response, "Nie udało się pobrać szczegółów służby.");
}

export async function createPlanningDuty(payload: PlanningDutyPayload): Promise<PlanningDutyDetails> {
    const response = await apiFetch("/api/planning/duties", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });

    return readJson<PlanningDutyDetails>(response, "Nie udało się dodać służby.");
}

export async function updatePlanningDuty(id: string, payload: PlanningDutyPayload): Promise<PlanningDutyDetails> {
    const response = await apiFetch(`/api/planning/duties/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });

    return readJson<PlanningDutyDetails>(response, "Nie udało się zapisać służby.");
}

export async function deletePlanningDuty(id: string): Promise<void> {
    const response = await apiFetch(`/api/planning/duties/${id}`, { method: "DELETE" });

    if (response.status === 404) {
        throw new Error("Nie znaleziono służby w Twojej firmie.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się usunąć służby.");
    }
}

export async function previewPlanningDutiesPdf(file: File): Promise<PlanningDutyPdfImportPreview> {
    const formData = new FormData();
    formData.append("file", file);

    const response = await apiFetch("/api/planning/duties/import/pdf/preview", {
        method: "POST",
        body: formData,
    });

    return readJson<PlanningDutyPdfImportPreview>(response, "Nie udało się przygotować podglądu importu PDF.");
}



export async function confirmPlanningDutiesPdfImport(
    request: PlanningDutyPdfImportConfirmRequest,
): Promise<PlanningDutyPdfImportConfirmResult> {
    const response = await apiFetch("/api/planning/duties/import/pdf/confirm", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });

    return readJson<PlanningDutyPdfImportConfirmResult>(response, "Nie udało się zapisać importu PDF do biblioteki.");
}
