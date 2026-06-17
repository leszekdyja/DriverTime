import { apiFetch } from "./apiClient";
import { getDddImports, type DddImport } from "./dddImportsService";
import type { DownloadDashboard } from "./downloadsService";

type DashboardSummary = {
    dddFilesCount: number;
    driverActivitiesCount: number;
    countryEntriesCount: number;
    vehicleUsesCount: number;
    overdueDriverDownloads: number;
    driverDownloadsDueIn7Days: number;
    downloadsDueIn7Days: number;
    driverDownloadsDueIn14Days: number;
    overdueVehicleDownloads: number;
    vehicleDownloadsDueIn7Days: number;
    vehicleDownloadsDueIn14Days: number;
    driversWithHighViolations: number;
    driversWithMediumViolations: number;
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
    alerts: DashboardAlerts;
};

export type DashboardAlerts = {
    overdueDriverDownloads: number;
    driverDownloadsDueIn7Days: number;
    downloadsDueIn7Days: number;
    driverDownloadsDueIn14Days: number;
    overdueVehicleDownloads: number;
    vehicleDownloadsDueIn7Days: number;
    vehicleDownloadsDueIn14Days: number;
    driversWithHighViolations: number;
    driversWithMediumViolations: number;
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

export type ComplianceRunDashboardStats = {
    generatedAtUtc: string;
    recentRunsCount: number;
    lastStatus: "NoData" | "Running" | "Completed" | string;
    lastRunAtUtc: string | null;
    lastRunViolationsCount: number;
    highViolationsCount: number;
    mediumViolationsCount: number;
    lowViolationsCount: number;
    driversInLastRunCount: number;
    schedulerEnabled: boolean;
    lastSchedulerRunAtUtc: string | null;
    lastSchedulerStatus: "NoData" | "Running" | "Completed" | string;
    lastSchedulerViolationsCount: number;
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
        alerts: {
            overdueDriverDownloads: summary.overdueDriverDownloads,
            driverDownloadsDueIn7Days: summary.driverDownloadsDueIn7Days,
            downloadsDueIn7Days: summary.downloadsDueIn7Days,
            driverDownloadsDueIn14Days: summary.driverDownloadsDueIn14Days,
            overdueVehicleDownloads: summary.overdueVehicleDownloads,
            vehicleDownloadsDueIn7Days: summary.vehicleDownloadsDueIn7Days,
            vehicleDownloadsDueIn14Days: summary.vehicleDownloadsDueIn14Days,
            driversWithHighViolations: summary.driversWithHighViolations,
            driversWithMediumViolations: summary.driversWithMediumViolations,
        },
    };
}

export function getDriverRiskOverview(): Promise<DriverRiskOverview> {
    return getJson<DriverRiskOverview>(
        "/api/dashboard/risk-overview",
        "Nie udało się pobrać danych ryzyka kierowców.",
    );
}

export function getComplianceRunDashboardStats(): Promise<ComplianceRunDashboardStats> {
    return getJson<ComplianceRunDashboardStats>(
        "/api/dashboard/compliance-runs",
        "Nie udało się pobrać statystyk compliance.",
    );
}

export function getDownloadDashboard(): Promise<DownloadDashboard> {
    return getJson<DownloadDashboard>(
        "/api/downloads/dashboard",
        "Nie udało się pobrać podsumowania terminów odczytów.",
    );
}

export async function checkApiHealth(): Promise<boolean> {
    const response = await apiFetch("/health");

    return response.ok;
}
