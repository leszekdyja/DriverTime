import { apiFetch } from "./apiClient";

export type AlertCategory = "Compliance" | "Downloads" | "Imports";
export type AlertSeverity = "Critical" | "Warning" | "Info";

export type AlertItem = {
    id: string;
    type: string;
    category: AlertCategory;
    severity: AlertSeverity;
    title: string;
    description: string;
    relatedEntityType: string;
    relatedEntityId: string | null;
    relatedEntityName: string;
    dueDateUtc: string | null;
    createdAtUtc: string;
    status: "Open" | string;
    actionUrl: string;
};

export async function getAlerts(): Promise<AlertItem[]> {
    const response = await apiFetch("/api/alerts");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać alertów.");
    }

    return response.json() as Promise<AlertItem[]>;
}
