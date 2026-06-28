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

export type VehicleActivity = {
    id: string;
    dddFileId: string;
    driverId: string | null;
    driverName: string | null;
    activityType: string;
    startUtc: string;
    endUtc: string;
    distanceKm?: number | null;
    startOdometerKm?: number | null;
    endOdometerKm?: number | null;
};

export type VehicleDetails = Vehicle & {
    lastActivityAtUtc: string | null;
    dddImportsCount: number;
    vehicleUses: VehicleUseHistory[];
    drivers: VehicleDriver[];
    activities: VehicleActivity[];
};

export type VehicleDetailsDateRange = {
    from?: string;
    to?: string;
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

export async function deleteVehicle(id: string): Promise<void> {
    const response = await apiFetch(`/api/vehicles/${id}`, { method: "DELETE" });

    if (response.status === 404) {
        throw new Error("Nie znaleziono pojazdu w Twojej firmie.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się usunąć pojazdu.");
    }
}
export async function getVehicle(id: string, range?: VehicleDetailsDateRange): Promise<VehicleDetails> {
    const params = new URLSearchParams();

    if (range?.from) {
        params.set("from", range.from);
    }

    if (range?.to) {
        params.set("to", range.to);
    }

    const query = params.toString();
    const response = await apiFetch(`/api/vehicles/${id}${query ? `?${query}` : ""}`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono pojazdu.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać pojazdu.");
    }

    return response.json() as Promise<VehicleDetails>;
}

export async function getVehicleAnalytics(id: string, range?: VehicleDetailsDateRange): Promise<VehicleAnalytics> {
    const params = new URLSearchParams();

    if (range?.from) {
        params.set("from", range.from);
    }

    if (range?.to) {
        params.set("to", range.to);
    }

    const query = params.toString();
    const response = await apiFetch(`/api/vehicles/${id}/analytics${query ? `?${query}` : ""}`);

    if (response.status === 404) {
        throw new Error("Nie znaleziono analityki pojazdu.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać analityki pojazdu.");
    }

    return response.json() as Promise<VehicleAnalytics>;
}
