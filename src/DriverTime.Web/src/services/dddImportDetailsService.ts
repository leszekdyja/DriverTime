import { API_URL } from "../config/api";

export type DriverActivity = {
    start: string;
    end: string;
    activity: string;
    activity_code: string;
    vehicle_registration: string;
    source: string;
};

export type CountryEntry = {
    timestamp: string;
    entry_type: string;
    country_code: string;
    country_name: string;
    status: string;
    related_day: string;
    note: string;
    source: string;
};

export type VehicleUse = {
    start: string;
    end: string;
    vehicle_registration: string;
    source: string;
};

export type DddImportDetails = {
    id: string;
    fileName: string;
    driverFirstName: string;
    driverLastName: string;
    driverCardNumber: string;
    uploadedAtUtc: string;
    driverActivities: DriverActivity[];
    countryEntries: CountryEntry[];
    vehicleUses: VehicleUse[];
};

export async function getDddImportDetails(
    id: string,
): Promise<DddImportDetails> {
    const response = await fetch(`${API_URL}/api/ddd-files/${id}`);

    if (!response.ok) {
        if (response.status === 404) {
            throw new Error("Nie znaleziono wybranego importu DDD.");
        }

        throw new Error("Nie udalo sie pobrac szczegolow importu DDD.");
    }

    return response.json() as Promise<DddImportDetails>;
}
