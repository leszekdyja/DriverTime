import { apiFetch } from "./apiClient";

export type LocalPlanningMatch = {
    sourceId: number;
    label: string;
    status: "Missing" | "Suggested" | "Ambiguous";
    candidateIds: string[];
};

export type LocalPlanningSyncPreview = {
    sourceId: string;
    exportedAtUtc: string;
    driversCount: number;
    dutiesCount: number;
    entriesCount: number;
    manualEntriesCount: number;
    possibleManualConflicts: number;
    possibleGeneratedConflicts: number;
    months: string[];
    driverSuggestions: LocalPlanningMatch[];
    dutySuggestions: LocalPlanningMatch[];
    settingsCounts: Record<string, number>;
    warnings: string[];
    isReadOnly: boolean;
};

export type LocalPlanningOption = { id: string; label: string };
export type LocalPlanningImportOptions = {
    operatingCompanyId: string | null; canChooseCompany: boolean;
    companies: LocalPlanningOption[]; drivers: LocalPlanningOption[]; duties: LocalPlanningOption[];
    schedules: (LocalPlanningOption & { year: number; month: number })[];
};
export type LocalPlanningImportRequest = {
    snapshot: unknown; operationId: string; year: number; month: number;
    operatingCompanyId: string | null; scheduleId: string | null;
    driverMappings: Record<string, string>; dutyMappings: Record<string, string>;
    decisions: Record<string, string>; allowSkipping: boolean; acknowledgeDefinitions: boolean; revisionToken: string;
};
export type LocalPlanningImportPlan = {
    revisionToken: string; scheduleId: string | null; selectedEntries: number; skippedEntries: number;
    newEntries: number; unchangedEntries: number; canApply: boolean; warnings: string[];
    conflicts: { sourceEntryId: number; date: string; driver: string; localDuty: string; serverDuty: string; serverStatus: string; decision: string | null }[];
};
export type LocalPlanningImportResult = { batchId: string; scheduleId: string; created: number; updated: number; keptServer: number; unchanged: number; skipped: number };
export type LocalPlanningBatch = { id: string; scheduleId: string; year: number; month: number; createdAtUtc: string; revertedAtUtc: string | null };

export async function localSyncRequest<T>(path: string, body?: unknown): Promise<T> {
    const response = await apiFetch(`/api/planning/local-sync/${path}`, body === undefined ? {} : {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body),
    });
    if (!response.ok) {
        const data = await response.json().catch(() => null);
        throw new Error(data?.message ?? "Nie udało się wykonać ręcznej operacji synchronizacji.");
    }
    return response.json();
}

export async function previewLocalPlanningSync(file: File, signal?: AbortSignal): Promise<LocalPlanningSyncPreview> {
    if (file.size > 16 * 1024 * 1024) throw new Error("Eksport może mieć najwyżej 16 MB.");
    const text = await file.text();
    try { JSON.parse(text); } catch { throw new Error("Wybierz prawidłowy plik JSON z eksportu planera lokalnego."); }
    const response = await apiFetch("/api/planning/local-sync/preview", {
        method: "POST", headers: { "Content-Type": "application/json" }, body: text, signal,
    });
    if (!response.ok) {
        const body = await response.json().catch(() => null);
        throw new Error(body?.message ?? "Nie udało się przygotować podglądu synchronizacji.");
    }
    return response.json();
}
