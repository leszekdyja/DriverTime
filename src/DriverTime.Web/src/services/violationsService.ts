import { apiFetch } from "./apiClient";

export type DriverViolation = {
    id: string;
    driverId: string;
    code: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    violationType: string;
    occurredAtUtc: string;
    periodEndUtc: string;
    description: string;
    severity: string;
    recommendation: string;
    detectedAtUtc: string;
    status?: string;
    actualDurationMinutes: number;
    limitDurationMinutes: number;
};

export type ViolationFilters = {
    driverId?: string;
    fromDate?: string;
    toDate?: string;
    severity?: string;
    type?: string;
};

function buildQuery(filters?: ViolationFilters) {
    const params = new URLSearchParams();

    if (!filters) {
        return "";
    }

    Object.entries(filters).forEach(([key, value]) => {
        if (value) {
            params.set(key, value);
        }
    });

    const query = params.toString();

    return query ? `?${query}` : "";
}

export async function getDriverViolations(
    filters?: ViolationFilters,
): Promise<DriverViolation[]> {
    const response = await apiFetch(`/api/violations${buildQuery(filters)}`);

    if (!response.ok) {
        throw new Error("Nie udało się pobrać naruszeń kierowców.");
    }

    return response.json() as Promise<DriverViolation[]>;
}

export async function getViolation(id: string): Promise<DriverViolation> {
    const response = await apiFetch(`/api/violations/${id}`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono naruszenia.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać szczegółów naruszenia.");
    }

    return response.json() as Promise<DriverViolation>;
}
