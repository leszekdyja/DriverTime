import { apiFetch } from "./apiClient";

export type Driver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
    cardExpiryDate: string | null;
    cardIssuingCountry: string;
    includeInPlanning: boolean;
    planningNoNightDuty: boolean;
    planningNoWeekends: boolean;
    planningNoSaturdays: boolean;
    planningNoHolidays: boolean;
    planningNoDaysOff: boolean;
};

export type DriverPlanningSettings = Pick<Driver, "includeInPlanning" | "planningNoNightDuty" | "planningNoWeekends" | "planningNoSaturdays" | "planningNoHolidays" | "planningNoDaysOff">;

export async function getDrivers(): Promise<Driver[]> {
    const response = await apiFetch("/api/drivers");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać kierowców.");
    }

    return response.json() as Promise<Driver[]>;
}

export async function updateDriverPlanning(id: string, settings: DriverPlanningSettings): Promise<Driver> {
    const response = await apiFetch(`/api/drivers/${id}/planning`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(settings),
    });

    if (!response.ok) {
        throw new Error("Nie udało się zapisać ustawienia planowania kierowcy.");
    }

    return response.json() as Promise<Driver>;
}
