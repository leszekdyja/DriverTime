export interface DddFileListItemDto {
    id: string;
    fileName: string;
    driverName: string;
    driverCardNumber: string;
    importedAt: string;
    activitiesCount: number;
}

export interface DriverActivityDto {
    id: string;
    start: string;
    end: string;
    activity: string;
    activityCode: string;
    vehicleRegistration: string;
    source: string;
}

export interface DashboardDto {
    driverName: string;
    driverCardNumber: string;
    totalDrivingTimeMinutes: number;
    totalWorkTimeMinutes: number;
    totalRestTimeMinutes: number;
    totalAvailabilityTimeMinutes: number;
    activitiesCount: number;
    firstActivityStart: string | null;
    lastActivityEnd: string | null;
}