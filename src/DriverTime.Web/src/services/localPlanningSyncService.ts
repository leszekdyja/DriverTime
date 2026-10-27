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
