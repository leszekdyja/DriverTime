import { API_URL } from "../config/api";

export type DriverViolation = {
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    violationType: string;
    occurredAtUtc: string;
    description: string;
    severity: "low" | "medium" | "high";
};

export async function getDriverViolations(): Promise<DriverViolation[]> {
    const response = await fetch(`${API_URL}/api/driver-violations`);

    if (!response.ok) {
        throw new Error("Nie udalo sie pobrac naruszen kierowcow.");
    }

    return response.json() as Promise<DriverViolation[]>;
}
