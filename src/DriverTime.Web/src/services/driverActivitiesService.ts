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
    vehicleRegistration?: string;
    vehicleRegistrationNumber?: string;
    vehicle?: string;
    startOdometerKm?: number | null;
    endOdometerKm?: number | null;
    distanceKm?: number | null;
};

export async function getDriverActivitiesByCard(
    driverCardNumber: string,
    from?: string,
    to?: string,
): Promise<DriverActivity[]> {
    const parameters = new URLSearchParams();
    const safeDriverCardNumber = driverCardNumber.trim();

    if (!safeDriverCardNumber) {
        return [];
    }

    parameters.set("driverCardNumber", safeDriverCardNumber);

    if (from) {
        parameters.set("from", from);
    }

    if (to) {
        parameters.set("to", to);
    }

    const response = await apiFetch(`/api/driver-activities?${parameters.toString()}`);

    if (!response.ok) {
        throw new Error("Nie udało się pobrać aktywności kierowcy.");
    }

    return (await response.json()) as DriverActivity[];
}
