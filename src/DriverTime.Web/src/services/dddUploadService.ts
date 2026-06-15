import { API_URL } from "../config/api";
import { getAuthToken } from "./apiClient";

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
            if (request.status < 200 || request.status >= 300) {
                reject(
                    new Error(
                        request.responseText || "Nie udalo sie przeslac pliku DDD.",
                    ),
                );
                return;
            }

            try {
                resolve(JSON.parse(request.responseText) as DddUploadResult);
            } catch {
                reject(new Error("Serwer zwrocil nieprawidlowa odpowiedz."));
            }
        });

        request.addEventListener("error", () => {
            reject(new Error("Nie udalo sie polaczyc z serwerem."));
        });

        request.addEventListener("abort", () => {
            reject(new Error("Przesylanie pliku zostalo przerwane."));
        });

        request.send(formData);
    });
}
