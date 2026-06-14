import { API_BASE_URL } from "../config/api";

export async function apiGet<T>(url: string): Promise<T> {
    const response = await fetch(`${API_BASE_URL}${url}`);

    if (!response.ok) {
        const errorText = await response.text();

        throw new Error(
            errorText || "Błąd pobierania danych z API.");
    }

    return response.json() as Promise<T>;
}

export async function apiPostForm<T>(
    url: string,
    formData: FormData
): Promise<T> {
    const response = await fetch(
        `${API_BASE_URL}${url}`,
        {
            method: "POST",
            body: formData
        });

    if (!response.ok) {
        const errorText = await response.text();

        throw new Error(
            errorText || "Błąd wysyłania danych do API.");
    }

    return response.json() as Promise<T>;
}