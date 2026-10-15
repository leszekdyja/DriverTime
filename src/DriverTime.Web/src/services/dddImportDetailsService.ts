import { apiFetch } from "./apiClient";

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
    fileType: "driver_card" | "vehicle_unit";
    vehicleRegistrationNumber: string;
    vehicleIdentificationNumber: string;
    vehicleUnit?: VehicleUnitData | null;
};

export type VehicleUnitDriverSession = {
    holder_surname?: string;
    holder_first_names?: string;
    card?: { card_number?: string; nation?: string };
    insertion_time?: string;
    withdrawal_time?: string;
    odometer_insertion_km?: number;
    odometer_withdrawal_km?: number;
    card_slot?: number;
};

export type VehicleUnitEvent = {
    description?: string;
    begin_time?: string;
    end_time?: string;
};

export type VehicleUnitData = {
    generation?: string;
    integrity_check?: string;
    coverage_percent?: number;
    vin?: string;
    registration_number?: string;
    registration_country?: string;
    downloaded_at?: string;
    daily_activities?: Array<{ date?: string; odometer_km?: number; changes_count?: number }>;
    driver_sessions?: VehicleUnitDriverSession[];
    events?: VehicleUnitEvent[];
    faults?: VehicleUnitEvent[];
    calibrations?: unknown[];
    places?: unknown[];
    overspeeding_events?: unknown[];
};

export async function getDddImportDetails(
    id: string,
): Promise<DddImportDetails> {
    const response = await apiFetch(`/api/ddd-files/${id}`);

    if (!response.ok) {
        if (response.status === 404) {
            throw new Error("Nie znaleziono wybranego importu DDD.");
        }

        throw new Error("Nie udało się pobrać szczegółów importu DDD.");
    }

    return response.json() as Promise<DddImportDetails>;
}
