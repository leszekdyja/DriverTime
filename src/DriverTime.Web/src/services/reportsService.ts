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
    vehicle?: string;
    vehicleRegistration?: string;
    vehicleRegistrationNumber?: string;
    startOdometerKm?: number | null;
    endOdometerKm?: number | null;
    distanceKm?: number | null;
};

type ReportFormat = "pdf" | "excel";

const reportFiles: Record<ReportFormat, { contentType: string; fallbackName: string }> = {
    pdf: {
        contentType: "application/pdf",
        fallbackName: "raport-kierowcy.pdf",
    },
    excel: {
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fallbackName: "raport-kierowcy.xlsx",
    },
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
        "Nie udało się pobrać listy kierowców.",
    );
}

export function getReportActivities(
    driverId: string,
    dateFrom: string,
    dateTo: string,
    driverCardNumber?: string,
): Promise<ReportActivity[]> {
    const parameters = new URLSearchParams();
    const safeDriverId = driverId.trim();
    const safeDriverCardNumber = driverCardNumber?.trim() ?? "";
    const safeDateFrom = dateFrom.trim();
    const safeDateTo = dateTo.trim();

    if ((!safeDriverId && !safeDriverCardNumber) || !safeDateFrom || !safeDateTo) {
        throw new Error("Wybierz kierowcę i pełny zakres dat przed pobraniem aktywności.");
    }

    if (safeDriverId) {
        parameters.set("driverId", safeDriverId);
    }

    if (safeDriverCardNumber) {
        parameters.set("driverCardNumber", safeDriverCardNumber);
    }

    parameters.set("from", `${safeDateFrom}T00:00:00Z`);
    parameters.set("to", `${safeDateTo}T23:59:59Z`);

    return getJson<ReportActivity[]>(
        `${API_URL}/api/driver-activities?${parameters.toString()}`,
        "Nie udało się pobrać danych raportu.",
    );
}

export async function downloadDriverReport(
    driverId: string,
    dateFrom: string,
    dateTo: string,
    format: ReportFormat,
): Promise<void> {
    const parameters = new URLSearchParams({
        from: dateFrom,
        to: dateTo,
    });
    const response = await apiFetch(
        `/api/reports/driver/${driverId}/export/${format}?${parameters.toString()}`,
    );

    if (!response.ok) {
        let message = `Nie udało się pobrać raportu ${format.toUpperCase()}.`;

        try {
            const error = (await response.json()) as { message?: string };
            message = error.message || message;
        } catch {
            // Response may not contain JSON.
        }

        throw new Error(message);
    }

    const file = reportFiles[format];
    const responseType = response.headers.get("Content-Type")?.split(";", 1)[0];

    if (responseType && responseType !== file.contentType) {
        throw new Error(`API zwróciło nieprawidłowy format pliku ${format.toUpperCase()}.`);
    }

    const blob = await response.blob();

    if (blob.size === 0) {
        throw new Error(`Pobrany plik ${format.toUpperCase()} jest pusty.`);
    }

    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    link.href = url;
    link.download = getDownloadFileName(
        response.headers.get("Content-Disposition"),
        file.fallbackName,
    );
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    link.remove();

    window.setTimeout(() => URL.revokeObjectURL(url), 1_000);
}

function getDownloadFileName(contentDisposition: string | null, fallback: string) {
    if (!contentDisposition) return fallback;

    const utf8Name = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];

    if (utf8Name) {
        try {
            return decodeURIComponent(utf8Name.replace(/^"|"$/g, ""));
        } catch {
            return fallback;
        }
    }

    return contentDisposition.match(/filename="?([^";]+)"?/i)?.[1] ?? fallback;
}
