import { apiFetch } from "./apiClient";

export type Vehicle = {
    id: string;
    registrationNumber: string;
    vin: string;
    active: boolean;
};

export type VehicleUseHistory = {
    id: string;
    dddFileId: string;
    fileName: string;
    uploadedAtUtc: string;
    driverId: string | null;
    driverName: string;
    registrationNumber: string;
    startUtc: string;
    endUtc: string;
};

export type VehicleDriver = {
    driverId: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    firstUsedAtUtc: string;
    lastUsedAtUtc: string;
    usageCount: number;
};

export type VehicleDetails = Vehicle & {
    lastActivityAtUtc: string | null;
    dddImportsCount: number;
    vehicleUses: VehicleUseHistory[];
    drivers: VehicleDriver[];
};

export type VehicleDailyUsage = {
    date: string;
    usesCount: number;
    usageMinutes: number;
};

export type VehicleDriverUsage = {
    driverId: string;
    driverName: string;
    cardNumber: string;
    usesCount: number;
    usageMinutes: number;
    firstUseUtc: string;
    lastUseUtc: string;
};

export type VehicleAnalytics = {
    vehicleId: string;
    registrationNumber: string;
    totalUses: number;
    totalDrivers: number;
    totalDddImports: number;
    firstUseUtc: string | null;
    lastUseUtc: string | null;
    totalUsageMinutes: number;
    totalUsageHours: number;
    activeDays: number;
    averageUsageMinutesPerActiveDay: number;
    usesLast7Days: number;
    usesLast30Days: number;
    dailyUsageLast30Days: VehicleDailyUsage[];
    driverUsage: VehicleDriverUsage[];
};

export async function getVehicles(): Promise<Vehicle[]> {
    const response = await apiFetch("/api/vehicles");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać pojazdów.");
    }

    return response.json() as Promise<Vehicle[]>;
}

export async function getVehicle(id: string): Promise<VehicleDetails> {
    const response = await apiFetch(`/api/vehicles/${id}`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono pojazdu.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać pojazdu.");
    }

    return response.json() as Promise<VehicleDetails>;
}

export async function getVehicleAnalytics(id: string): Promise<VehicleAnalytics> {
    const response = await apiFetch(`/api/vehicles/${id}/analytics`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono analityki pojazdu.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać analityki pojazdu.");
    }

    return response.json() as Promise<VehicleAnalytics>;
}
