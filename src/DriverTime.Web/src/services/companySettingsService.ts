import { apiFetch } from "./apiClient";

export type CompanySettings = {
    name: string;
    vatNumber: string;
    address: string;
    email: string;
    phone: string;
};

async function readError(response: Response, fallback: string) {
    try {
        const error = (await response.json()) as { message?: string };
        return error.message || fallback;
    } catch {
        return fallback;
    }
}

export async function getCompanySettings(): Promise<CompanySettings> {
    const response = await apiFetch("/api/company/settings");

    if (!response.ok) {
        throw new Error(await readError(response, "Nie udalo sie pobrac ustawien firmy."));
    }

    return response.json() as Promise<CompanySettings>;
}

export async function updateCompanySettings(
    settings: CompanySettings,
): Promise<CompanySettings> {
    const response = await apiFetch("/api/company/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(settings),
    });

    if (!response.ok) {
        throw new Error(await readError(response, "Nie udalo sie zapisac ustawien firmy."));
    }

    return response.json() as Promise<CompanySettings>;
}
