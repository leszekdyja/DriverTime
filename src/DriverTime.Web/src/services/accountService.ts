import { apiFetch } from "./apiClient";

export type AccountProfile = {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    role: string;
    companyName: string;
};

export type UpdateAccountProfile = Pick<AccountProfile, "firstName" | "lastName" | "email">;

export type ChangePassword = {
    currentPassword: string;
    newPassword: string;
};

async function readError(response: Response, fallback: string) {
    try {
        const body = (await response.json()) as { message?: string; title?: string };
        return body.message || body.title || fallback;
    } catch {
        return fallback;
    }
}

export async function getAccountProfile(): Promise<AccountProfile> {
    const response = await apiFetch("/api/account/profile");

    if (!response.ok) {
        throw new Error(await readError(response, "Nie udalo sie pobrac profilu."));
    }

    return response.json() as Promise<AccountProfile>;
}

export async function updateAccountProfile(
    profile: UpdateAccountProfile,
): Promise<AccountProfile> {
    const response = await apiFetch("/api/account/profile", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(profile),
    });

    if (!response.ok) {
        throw new Error(await readError(response, "Nie udalo sie zapisac profilu."));
    }

    return response.json() as Promise<AccountProfile>;
}

export async function changeAccountPassword(passwords: ChangePassword): Promise<void> {
    const response = await apiFetch("/api/account/change-password", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(passwords),
    });

    if (!response.ok) {
        throw new Error(await readError(response, "Nie udalo sie zmienic hasla."));
    }
}
