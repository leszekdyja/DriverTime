import { apiFetch } from "./apiClient";
import type { DriverViolation } from "./violationsService";
import { getComplianceRuleLabel } from "../utils/complianceLabels";

export type ComplianceTimelineEntry = {
    sourceActivityId: string;
    activityType: string;
    startUtc: string;
    endUtc: string;
    durationMinutes: number;
};

export type ComplianceViolationPreview = {
    code: string;
    ruleName: string;
    severity: string;
    description: string;
    periodStartUtc: string;
    periodEndUtc: string;
    actualMinutes: number;
    limitMinutes: number;
    metadata: Record<string, number | string | null>;
};

export type ComplianceDebugSummary = {
    drivingMinutesByDay: Record<string, number>;
    restMinutesByDay: Record<string, number>;
    weeklyDrivingMinutes: Record<string, number>;
    biWeeklyDrivingMinutes: Record<string, number>;
    registeredRuleCodes: string[];
};

export type CompliancePreview = {
    driverId: string;
    timelineCount: number;
    violationsCount: number;
    timeline: ComplianceTimelineEntry[];
    violations: ComplianceViolationPreview[];
    debugSummary: ComplianceDebugSummary;
};

export type ComplianceDriver = {
    id: string;
    firstName: string;
    lastName: string;
    cardNumber: string;
};

export async function getDrivers(): Promise<ComplianceDriver[]> {
    const response = await apiFetch("/api/drivers");

    if (!response.ok) {
        throw new Error("Nie udało się pobrać kierowców.");
    }

    return response.json() as Promise<ComplianceDriver[]>;
}

export async function getCompliancePreview(
    driverId: string,
    includeTimeline = false,
): Promise<CompliancePreview> {
    const response = await apiFetch(
        `/api/compliance/drivers/${driverId}/preview?includeTimeline=${includeTimeline}`,
    );

    if (response.status === 404) {
        throw new Error("Nie znaleziono danych compliance dla kierowcy.");
    }

    if (!response.ok) {
        throw new Error("Nie udało się pobrać podglądu compliance.");
    }

    return response.json() as Promise<CompliancePreview>;
}

export function mapComplianceViolation(
    driver: ComplianceDriver,
    violation: ComplianceViolationPreview,
    index: number,
): DriverViolation {
    return {
        id: `${driver.id}-${violation.code}-${violation.periodStartUtc}-${index}`,
        driverId: driver.id,
        code: violation.code,
        driverFirstName: driver.firstName,
        driverLastName: driver.lastName,
        driverCardNumber: driver.cardNumber,
        violationType: getComplianceRuleLabel(violation.ruleName, violation.code),
        occurredAtUtc: violation.periodStartUtc,
        periodEndUtc: violation.periodEndUtc,
        description: violation.description,
        severity: violation.severity,
        recommendation: "",
        detectedAtUtc: violation.periodStartUtc,
        status: "open",
        actualDurationMinutes: violation.actualMinutes,
        limitDurationMinutes: violation.limitMinutes,
        metadata: violation.metadata,
    };
}

export async function getComplianceViolationsForDriver(
    driver: ComplianceDriver,
): Promise<DriverViolation[]> {
    const preview = await getCompliancePreview(driver.id);

    return preview.violations.map((violation, index) =>
        mapComplianceViolation(driver, violation, index),
    );
}

export async function getComplianceViolationsForDrivers(
    drivers: ComplianceDriver[],
): Promise<DriverViolation[]> {
    const violationsByDriver = await Promise.all(
        drivers.map((driver) => getComplianceViolationsForDriver(driver)),
    );

    return violationsByDriver.flat();
}
