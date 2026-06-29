import { apiFetch } from "./apiClient";

export type Driver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
};

export async function getDrivers(): Promise<Driver[]> {
    const response = await apiFetch("/api/drivers");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać kierowców.");
    }

    return response.json() as Promise<Driver[]>;
}
