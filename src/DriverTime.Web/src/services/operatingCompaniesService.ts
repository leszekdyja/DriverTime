import { apiFetch } from "./apiClient";

export type OperatingCompany = {
    id: string;
    name: string;
    taxNumber: string;
    active: boolean;
    driversCount: number;
    createdAtUtc: string;
};

export type SaveOperatingCompanyRequest = Pick<OperatingCompany, "name" | "taxNumber" | "active">;

async function parseError(response: Response, fallback: string) {
    const body = await response.json().catch(() => null) as { message?: string } | null;
    return body?.message ?? fallback;
}

export async function getOperatingCompanies(): Promise<OperatingCompany[]> {
    const response = await apiFetch("/api/operating-companies");
    if (!response.ok) throw new Error("Nie udało się pobrać firm.");
    return response.json() as Promise<OperatingCompany[]>;
}

export async function createOperatingCompany(request: SaveOperatingCompanyRequest): Promise<OperatingCompany> {
    const response = await apiFetch("/api/operating-companies", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
    if (!response.ok) throw new Error(await parseError(response, "Nie udało się utworzyć firmy."));
    return response.json() as Promise<OperatingCompany>;
}

export async function updateOperatingCompany(id: string, request: SaveOperatingCompanyRequest): Promise<OperatingCompany> {
    const response = await apiFetch(`/api/operating-companies/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
    if (!response.ok) throw new Error(await parseError(response, "Nie udało się zapisać firmy."));
    return response.json() as Promise<OperatingCompany>;
}

export async function deleteOperatingCompany(id: string): Promise<void> {
    const response = await apiFetch(`/api/operating-companies/${id}`, { method: "DELETE" });
    if (!response.ok) throw new Error("Nie udało się usunąć firmy.");
}
