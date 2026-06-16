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
    imports: DddImport[];
    latestImports: DddImport[];
    activities: DriverActivity[];
};

export type DriverRisk = {
    driverId: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    violationsCount: number;
    severeViolationsCount: number;
    lastImportAtUtc: string | null;
    lastActivityAtUtc: string | null;
    daysSinceLastImport: number | null;
    daysSinceLastActivity: number | null;
    riskStatus: "Low" | "Medium" | "High" | "Critical";
    riskScore: number;
};

export type DriverRiskOverview = {
    generatedAtUtc: string;
    lowRiskCount: number;
    mediumRiskCount: number;
    highRiskCount: number;
    criticalRiskCount: number;
    drivers: DriverRisk[];
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
            "Nie udało się pobrać podsumowania dashboardu.",
        ),
        getJson<Driver[]>(
            "/api/drivers",
            "Nie udało się pobrać listy kierowców.",
        ),
        getDddImports(),
        getJson<DriverActivity[]>(
            "/api/driver-activities",
            "Nie udało się pobrać statystyk aktywności.",
        ),
    ]);

    const latestImports = imports.slice(0, 5);

    return {
        totalImports: summary.dddFilesCount,
        totalDrivers: drivers.length,
        totalActivities: summary.driverActivitiesCount,
        totalVehicles: summary.vehicleUsesCount,
        latestImportDate: latestImports[0]?.uploadedAtUtc ?? null,
        imports,
        latestImports,
        activities,
    };
}

export function getDriverRiskOverview(): Promise<DriverRiskOverview> {
    return getJson<DriverRiskOverview>(
        "/api/dashboard/risk-overview",
        "Nie udało się pobrać danych ryzyka kierowców.",
    );
}

export async function checkApiHealth(): Promise<boolean> {
    const response = await apiFetch("/health");

    return response.ok;
}
