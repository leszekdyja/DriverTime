import { API_URL } from "../config/api";

export type DddUploadResult = {
    parser_name: string;
    parser_version: string;
    file_type: string;
    card_read_date: string;
    activities: unknown[];
    vehicle_uses: unknown[];
    country_code_entries: unknown[];
};

async function getErrorMessage(response: Response) {
    const message = await response.text();

    return message || "Nie udalo sie przeslac pliku DDD.";
}

export async function uploadDddFile(file: File): Promise<DddUploadResult> {
    const formData = new FormData();
    formData.append("file", file);

    const response = await fetch(`${API_URL}/api/ddd-files/upload`, {
        method: "POST",
        body: formData,
    });

    if (!response.ok) {
        throw new Error(await getErrorMessage(response));
    }

    return response.json() as Promise<DddUploadResult>;
}
