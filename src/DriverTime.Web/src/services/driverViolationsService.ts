import {
    getDriverViolations as getViolations,
    type DriverViolation,
} from "./violationsService";

export type { DriverViolation };

export async function getDriverViolations(
    driverId: string,
): Promise<DriverViolation[]> {
    return getViolations({ driverId });
}
