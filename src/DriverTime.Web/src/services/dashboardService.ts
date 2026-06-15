import { apiFetch } from "./apiClient";
import { getDddImports, type DddImport } from "./dddImportsService";

type DashboardSummary = {
    dddFilesCount: number;
    driverActivitiesCount: number;
    countryEntriesCount: number;
    vehicleUsesCount: number;
    generatedAtUtc: string;
};

type Driver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
};

export type DriverActivity = {
    id: string;
    dddFileId: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds: number;
};

export type DashboardData = {
    totalImports: number;
    totalDrivers: number;
    totalActivities: number;
    totalVehicles: number;
    latestImportDate: string | null;
    latestImports: DddImport[];
    activities: DriverActivity[];
};

async function getJson<T>(path: string, errorMessage: string): Promise<T> {
    const response = await apiFetch(path);

    if (!response.ok) {
        throw new Error(errorMessage);
    }

    return response.json() as Promise<T>;
}

export async function getDashboardData(): Promise<DashboardData> {
    const [summary, drivers, imports, activities] = await Promise.all([
        getJson<DashboardSummary>(
            "/api/dashboard",
            "Nie udalo sie pobrac podsumowania dashboardu.",
        ),
        getJson<Driver[]>(
            "/api/drivers",
            "Nie udalo sie pobrac listy kierowcow.",
        ),
        getDddImports(),
        getJson<DriverActivity[]>(
            "/api/driver-activities",
            "Nie udalo sie pobrac statystyk aktywnosci.",
        ),
    ]);

    const latestImports = imports.slice(0, 5);

    return {
        totalImports: summary.dddFilesCount,
        totalDrivers: drivers.length,
        totalActivities: summary.driverActivitiesCount,
        totalVehicles: summary.vehicleUsesCount,
        latestImportDate: latestImports[0]?.uploadedAtUtc ?? null,
        latestImports,
        activities,
    };
}
