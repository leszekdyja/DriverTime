import { apiFetch } from "./apiClient";

export type ImportMonitoringEntry = {
    id: string;
    fileName: string;
    status: "Pending" | "Processing" | "Completed" | "Failed" | string;
    errorMessage: string;
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
