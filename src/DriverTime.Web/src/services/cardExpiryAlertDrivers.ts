// The download API already verifies replacements within the authenticated company.
// If that request failed, retain expiry alerts rather than silently hiding them.
export function cardExpiryAlertDrivers<T extends { id: string; isActive?: boolean }>(
    drivers: readonly T[], activeDownloads: readonly { driverId: string }[] | null,
): T[] {
    const currentDrivers = drivers.filter(driver => driver.isActive !== false);
    if (activeDownloads === null) return currentDrivers;
    const activeIds = new Set(activeDownloads.map(download => download.driverId));
    return currentDrivers.filter(driver => activeIds.has(driver.id));
}
