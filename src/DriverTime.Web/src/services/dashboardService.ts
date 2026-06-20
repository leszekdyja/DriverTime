import { apiFetch } from "./apiClient";
import type { DddImport } from "./dddImportsService";
import type { DownloadDashboard } from "./downloadsService";

type DashboardSummary = {
    dddFilesCount: number;
    driversCount: number;
    vehiclesCount: number;
    violationsCount: number;
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
    rangeStartUtc: string;
    rangeEndUtc: string;
    activitySummaries: DashboardActivitySummary[];
    importTrend: DashboardImportTrend[];
    latestImports: DddImport[];
    violationSummaries: DashboardViolationSummary[];
    latestViolations: DashboardViolation[];
};

export type DashboardDriver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
};

export type DriverActivity = {
    id: string;
    dddFileId: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds: number;
};

export type DashboardActivitySummary = {
    activityType: string;
    count: number;
    durationSeconds: number;
};

export type DashboardImportTrend = {
    dayUtc: string;
    importsCount: number;
};

export type DashboardViolationSummary = {
    severity: string;
    count: number;
};

export type DashboardViolation = {
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
};

export type DashboardData = {
    totalImports: number;
    totalDrivers: number;
    totalActivities: number;
    totalVehicles: number;
    totalViolations: number;
    latestImportDate: string | null;
    latestImports: DddImport[];
    activitySummaries: DashboardActivitySummary[];
    importTrend: DashboardImportTrend[];
    violationSummaries: DashboardViolationSummary[];
    latestViolations: DashboardViolation[];
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
    const summary = await getJson<DashboardSummary>(
        "/api/dashboard",
        "Nie udało się pobrać podsumowania dashboardu.",
    );

    return {
        totalImports: summary.dddFilesCount,
        totalDrivers: summary.driversCount,
        totalActivities: summary.driverActivitiesCount,
        totalVehicles: summary.vehiclesCount,
        totalViolations: summary.violationsCount,
        latestImportDate: summary.latestImports[0]?.uploadedAtUtc ?? null,
        latestImports: summary.latestImports,
        activitySummaries: summary.activitySummaries,
        importTrend: summary.importTrend,
        violationSummaries: summary.violationSummaries,
        latestViolations: summary.latestViolations,
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

export function getDashboardDrivers(): Promise<DashboardDriver[]> {
    return getJson<DashboardDriver[]>(
        "/api/drivers",
        "Nie udało się pobrać listy kierowców.",
    );
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
