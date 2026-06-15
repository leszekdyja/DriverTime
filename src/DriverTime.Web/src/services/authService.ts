import { API_URL } from "../config/api";
import { apiFetch } from "./apiClient";

export type CurrentUser = {
    id: string;
    companyId: string;
    companyName: string;
    firstName: string;
    lastName: string;
    email: string;
    role: string;
};

export type AuthResponse = {
    token: string;
    expiresAtUtc: string;
    user: CurrentUser;
};

export async function login(email: string, password: string): Promise<AuthResponse> {
    const response = await fetch(`${API_URL}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
    });

    if (!response.ok) {
        throw new Error("Nieprawidlowy adres e-mail lub haslo.");
    }

    return response.json() as Promise<AuthResponse>;
}

export async function getCurrentUser(): Promise<CurrentUser> {
    const response = await apiFetch("/api/auth/me");

    if (!response.ok) {
        throw new Error("Sesja wygasla.");
    }

    return response.json() as Promise<CurrentUser>;
}
