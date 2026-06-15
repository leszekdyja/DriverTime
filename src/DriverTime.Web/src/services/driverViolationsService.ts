import { apiFetch } from "./apiClient";

export type DriverViolation = {
    code: string;
    violationType: string;
    occurredAtUtc: string;
    periodEndUtc: string;
    description: string;
    severity: string;
    actualDurationMinutes: number;
    limitDurationMinutes: number;
};

export async function getDriverViolations(
    driverId: string,
): Promise<DriverViolation[]> {
    const response = await apiFetch(`/api/drivers/${driverId}/violations`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono kierowcy.");
    }

    if (!response.ok) {
        throw new Error("Nie udalo sie pobrac naruszen kierowcy.");
    }

    return (await response.json()) as DriverViolation[];
}
