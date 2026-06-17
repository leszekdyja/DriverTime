import { apiFetch } from "./apiClient";

export type DddImport = {
    id: string;
    fileName: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    driverStatus: "new" | "existing";
    uploadedAtUtc: string;
    activitiesCount: number;
};

export async function getDddImports(): Promise<DddImport[]> {
    const response = await apiFetch("/api/ddd-files");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać listy importów DDD.");
    }

    return response.json() as Promise<DddImport[]>;
}

export async function deleteDddImport(id: string): Promise<void> {
    const response = await apiFetch(`/api/ddd-files/${id}`, {
        method: "DELETE",
    });

    if (response.status === 404) {
        throw new Error("Nie znaleziono importu DDD.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się usunąć importu DDD.");
    }
}
