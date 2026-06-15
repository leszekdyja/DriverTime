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
        throw new Error("Nie udalo sie pobrac listy importow DDD.");
    }

    return response.json() as Promise<DddImport[]>;
}
