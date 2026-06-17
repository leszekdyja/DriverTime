import { apiFetch } from "./apiClient";

export type DownloadStatus = "OK" | "Warning" | "Overdue";

export type DriverDownload = {
    driverId: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    lastDownloadUtc: string | null;
    nextRequiredDownloadUtc: string | null;
    daysUntilDue: number | null;
    status: DownloadStatus;
};

export type VehicleDownload = {
    vehicleId: string;
    registrationNumber: string;
    lastDownloadUtc: string | null;
    nextRequiredDownloadUtc: string | null;
    daysUntilDue: number | null;
    status: DownloadStatus;
};

export type DownloadDashboard = {
    overdueDrivers: number;
    warningDrivers: number;
    overdueVehicles: number;
    warningVehicles: number;
    nextDriversDue: DriverDownload[];
    nextVehiclesDue: VehicleDownload[];
};

async function getJson<T>(path: string, errorMessage: string): Promise<T> {
    const response = await apiFetch(path);

    if (!response.ok) {
        throw new Error(errorMessage);
    }

    return response.json() as Promise<T>;
}

export function getDriverDownloads(): Promise<DriverDownload[]> {
    return getJson<DriverDownload[]>(
        "/api/downloads/drivers",
        "Nie udało się pobrać terminów odczytów kierowców.",
    );
}

export function getVehicleDownloads(): Promise<VehicleDownload[]> {
    return getJson<VehicleDownload[]>(
        "/api/downloads/vehicles",
        "Nie udało się pobrać terminów odczytów pojazdów.",
    );
}

export function getDownloadDashboard(): Promise<DownloadDashboard> {
    return getJson<DownloadDashboard>(
        "/api/downloads/dashboard",
        "Nie udało się pobrać podsumowania odczytów.",
    );
}
