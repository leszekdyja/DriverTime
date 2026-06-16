import { apiFetch } from "./apiClient";

export type ImportMonitoringEntry = {
    id: string;
    fileName: string;
    status: "Pending" | "Processing" | "Completed" | "Failed" | string;
    errorMessage: string;
    retryCount: number;
    lastRetryAtUtc: string | null;
    lastError: string;
    startedAtUtc: string | null;
    finishedAtUtc: string | null;
    createdAtUtc: string;
    companyId: string | null;
    userId: string | null;
};

export async function getRecentImportMonitoring(
    take = 20,
): Promise<ImportMonitoringEntry[]> {
    const response = await apiFetch(`/api/import-monitoring/recent?take=${take}`);

    if (!response.ok) {
        throw new Error("Nie udało się pobrać monitoringu importów DDD.");
    }

    return response.json() as Promise<ImportMonitoringEntry[]>;
}

export async function retryImportMonitoringEntry(id: string): Promise<void> {
    const response = await apiFetch(`/api/import-monitoring/${id}/retry`, {
        method: "POST",
    });

    if (!response.ok) {
        throw new Error("Nie udało się ponowić importu DDD.");
    }
}

export async function retryFailedImportMonitoringEntries(): Promise<void> {
    const response = await apiFetch("/api/import-monitoring/retry-failed", {
        method: "POST",
    });

    if (!response.ok) {
        throw new Error("Nie udało się ponowić błędnych importów DDD.");
    }
}
