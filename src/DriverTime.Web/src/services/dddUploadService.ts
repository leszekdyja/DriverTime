import { API_URL } from "../config/api";
import { clearAuthSession, getAuthToken } from "./apiClient";

export type DddUploadResult = {
    importId: string;
    driverCreated: boolean;
    importMessage: string;
    driver: {
        first_name: string;
        last_name: string;
        card_number: string;
        card_expiry_date: string;
        card_issuing_country: string;
    };
    parser_name: string;
    parser_version: string;
    file_type: string;
    card_read_date: string;
    activities: unknown[];
    vehicle_uses: unknown[];
    country_code_entries: unknown[];
};

export function uploadDddFile(
    file: File,
    onProgress?: (progress: number) => void,
): Promise<DddUploadResult> {
    return new Promise((resolve, reject) => {
        const request = new XMLHttpRequest();
        const formData = new FormData();

        formData.append("file", file);
        request.open("POST", `${API_URL}/api/ddd-files/upload`);

        const token = getAuthToken();

        if (token) {
            request.setRequestHeader("Authorization", `Bearer ${token}`);
        }

        request.upload.addEventListener("progress", (event) => {
            if (event.lengthComputable) {
                onProgress?.(Math.round((event.loaded / event.total) * 100));
            }
        });

        request.addEventListener("load", () => {
            if (request.status === 401 && token) {
                clearAuthSession();
            }

            if (request.status < 200 || request.status >= 300) {
                reject(new Error(getUploadErrorMessage(request.responseText)));
                return;
            }

            try {
                resolve(JSON.parse(request.responseText) as DddUploadResult);
            } catch {
                reject(new Error("Serwer zwrocil nieprawidlowa odpowiedz."));
            }
        });

        request.addEventListener("error", () => {
            reject(new Error("Nie udało się połączyć z serwerem."));
        });

        request.addEventListener("abort", () => {
            reject(new Error("Przesyłanie pliku zostało przerwane."));
        });

        request.send(formData);
    });
}

function getUploadErrorMessage(responseText: string): string {
    const fallbackMessage = "Nie udało się przesłać pliku DDD.";

    if (!responseText.trim()) {
        return fallbackMessage;
    }

    try {
        const response = JSON.parse(responseText) as {
            message?: string;
            detail?: string;
            title?: string;
        };

        return response.message || response.detail || response.title || fallbackMessage;
    } catch {
        return responseText;
    }
}
