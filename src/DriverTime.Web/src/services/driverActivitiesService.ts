import { apiFetch } from "./apiClient";

export type DriverActivity = {
    id: string;
    dddFileId: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds: number;
};

export async function getDriverActivitiesByCard(
    driverCardNumber: string,
): Promise<DriverActivity[]> {
    const parameters = new URLSearchParams();
    const safeDriverCardNumber = driverCardNumber.trim();

    if (!safeDriverCardNumber) {
        return [];
    }

    parameters.set("driverCardNumber", safeDriverCardNumber);

    const response = await apiFetch(`/api/driver-activities?${parameters.toString()}`);

    if (!response.ok) {
        throw new Error("Nie udało się pobrać aktywności kierowcy.");
    }

    return (await response.json()) as DriverActivity[];
}
