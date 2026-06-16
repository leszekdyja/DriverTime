import { apiFetch } from "./apiClient";

export type DriverViolation = {
    code: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    violationType: string;
    occurredAtUtc: string;
    periodEndUtc: string;
    description: string;
    severity: string;
    status?: string;
    actualDurationMinutes: number;
    limitDurationMinutes: number;
};

export async function getDriverViolations(): Promise<DriverViolation[]> {
    const response = await apiFetch("/api/driver-violations");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać naruszeń kierowców.");
    }

    return response.json() as Promise<DriverViolation[]>;
}
