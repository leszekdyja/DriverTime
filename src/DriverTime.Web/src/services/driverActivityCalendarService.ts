import { apiFetch } from "./apiClient";
import type { DriverViolation } from "./driverViolationsService";

export type CalendarActivity = {
    id: string;
    startUtc: string;
    endUtc: string;
    activityType: string;
    durationSeconds: number;
};

export type ActivityCalendarDay = {
    date: string;
    drivingSeconds: number;
    workSeconds: number;
    restSeconds: number;
    availabilitySeconds: number;
    otherSeconds: number;
    activities: CalendarActivity[];
    violations: DriverViolation[];
};

export type DriverActivityCalendar = {
    driverId: string;
    from: string;
    to: string;
    days: ActivityCalendarDay[];
};

export async function getDriverActivityCalendar(
    driverId: string,
    from: string,
    to: string,
): Promise<DriverActivityCalendar> {
    const parameters = new URLSearchParams({ from, to });
    const response = await apiFetch(
        `/api/drivers/${driverId}/activity-calendar?${parameters.toString()}`,
    );

    if (response.status === 404) {
        throw new Error("Nie znaleziono kierowcy.");
    }

    if (!response.ok) {
        let message = "Nie udało się pobrać kalendarza aktywności.";

        try {
            const error = (await response.json()) as { message?: string };
            message = error.message || message;
        } catch {
            // Error response may not contain JSON.
        }

        throw new Error(message);
    }

    return (await response.json()) as DriverActivityCalendar;
}
