import { API_URL } from "../config/api";

export type DddImport = {
    id: string;
    fileName: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    uploadedAtUtc: string;
    activitiesCount: number;
};

export async function getDddImports(): Promise<DddImport[]> {
    const response = await fetch(`${API_URL}/api/ddd-files`);

    if (!response.ok) {
        throw new Error("Nie udalo sie pobrac listy importow DDD.");
    }

    return response.json() as Promise<DddImport[]>;
}
