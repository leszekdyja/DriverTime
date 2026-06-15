import { API_URL } from "../config/api";
import { apiFetch } from "./apiClient";

export type ReportDriver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
};

export type ReportActivity = {
    id: string;
    dddFileId: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds: number;
};

async function getJson<T>(url: string, errorMessage: string): Promise<T> {
    const response = await apiFetch(url);

    if (!response.ok) {
        throw new Error(errorMessage);
    }

    return response.json() as Promise<T>;
}

export function getReportDrivers(): Promise<ReportDriver[]> {
    return getJson<ReportDriver[]>(
        `${API_URL}/api/drivers`,
        "Nie udalo sie pobrac listy kierowcow.",
    );
}

export function getReportActivities(
    driverCardNumber: string,
    dateFrom: string,
    dateTo: string,
): Promise<ReportActivity[]> {
    const parameters = new URLSearchParams();

    if (driverCardNumber) {
        parameters.set("driverCardNumber", driverCardNumber);
    }

    if (dateFrom) {
        parameters.set("from", `${dateFrom}T00:00:00Z`);
    }

    if (dateTo) {
        parameters.set("to", `${dateTo}T23:59:59Z`);
    }

    return getJson<ReportActivity[]>(
        `${API_URL}/api/driver-activities?${parameters.toString()}`,
        "Nie udalo sie pobrac danych raportu.",
    );
}

export async function downloadDriverReport(
    driverId: string,
    dateFrom: string,
    dateTo: string,
    format: "pdf" | "excel",
): Promise<void> {
    const parameters = new URLSearchParams({
        from: dateFrom,
        to: dateTo,
    });
    const response = await apiFetch(
        `/api/reports/driver/${driverId}/export/${format}?${parameters.toString()}`,
    );

    if (!response.ok) {
        let message = `Nie udalo sie pobrac raportu ${format.toUpperCase()}.`;

        try {
            const error = (await response.json()) as { message?: string };
            message = error.message || message;
        } catch {
            // Response may not contain JSON.
        }

        throw new Error(message);
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    link.href = url;
    link.download = format === "pdf"
        ? "raport-kierowcy.pdf"
        : "raport-kierowcy.xlsx";
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}
