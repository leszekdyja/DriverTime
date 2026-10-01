import { apiFetch } from "./apiClient";

export type DriverImport = {
    id: string;
    fileName: string;
    uploadedAtUtc: string;
    activitiesCount: number;
};

export type DriverActivity = {
    id: string;
    activityType: string;
    startUtc: string;
    endUtc: string;
    durationSeconds: number;
};

export type DriverViolation = {
    violationType: string;
    occurredAtUtc: string;
    description: string;
    severity: string;
};

export type DriverCountryEntry = {
    id: string;
    entryTimeUtc: string;
    timeUtc?: string;
    countryCode: string;
    entryType?: string;
};

export type DriverVehicleUse = {
    id: string;
    startUtc: string;
    endUtc: string;
    registrationNumber: string;
    distanceKm?: number | null;
    startOdometerKm?: number | null;
    endOdometerKm?: number | null;
};

export type DriverVehicle = {
    registrationNumber: string;
    firstUsedAtUtc: string;
    lastUsedAtUtc: string;
    usageCount: number;
};

export type DriverDetails = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
    createdAtUtc: string;
    importsCount: number;
    lastImportAtUtc: string | null;
    drivingSeconds: number;
    workSeconds: number;
    restSeconds: number;
    availabilitySeconds: number;
    recentImports: DriverImport[];
    recentActivities: DriverActivity[];
    recentViolations: DriverViolation[];
    vehicles: DriverVehicle[];
    countryEntries: DriverCountryEntry[];
    vehicleUses: DriverVehicleUse[];
};

export async function getDriverDetails(id: string): Promise<DriverDetails> {
    const response = await apiFetch(`/api/drivers/${id}`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono kierowcy.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać szczegółów kierowcy.");
    }

    return (await response.json()) as DriverDetails;
}
