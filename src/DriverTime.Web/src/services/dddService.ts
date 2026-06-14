import { apiGet, apiPostForm } from "../api/httpClient";
import type {
    DashboardDto,
    DddFileListItemDto,
    DriverActivityDto
} from "../models/dddModels";

export async function uploadDddFile(file: File): Promise<unknown> {
    const formData = new FormData();
    formData.append("file", file);

    return apiPostForm<unknown>("/ddd-files/upload", formData);
}

export async function getDddImports(): Promise<DddFileListItemDto[]> {
    return apiGet<DddFileListItemDto[]>("/ddd-files");
}

export async function getDriverActivities(
    dddFileId: string
): Promise<DriverActivityDto[]> {
    return apiGet<DriverActivityDto[]>(
        `/driver-activities/${dddFileId}`);
}

export async function getDashboard(
    dddFileId: string
): Promise<DashboardDto> {
    return apiGet<DashboardDto>(
        `/dashboard/${dddFileId}`);
}