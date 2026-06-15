import { API_URL } from "../config/api";

export const AUTH_TOKEN_KEY = "drivertime.auth.token";

export function getAuthToken() {
    return localStorage.getItem(AUTH_TOKEN_KEY);
}

export function clearAuthSession() {
    localStorage.removeItem(AUTH_TOKEN_KEY);
    window.dispatchEvent(new Event("drivertime:logout"));
}

export async function apiFetch(path: string, init: RequestInit = {}) {
    const headers = new Headers(init.headers);
    const token = getAuthToken();

    if (token) {
        headers.set("Authorization", `Bearer ${token}`);
    }

    let response: Response;

    try {
        response = await fetch(path.startsWith("http") ? path : `${API_URL}${path}`, {
            ...init,
            headers,
        });
    } catch {
        throw new Error("Nie udalo sie polaczyc z API DriverTime.");
    }

    if (response.status === 401 && token) {
        clearAuthSession();
    }

    return response;
}
