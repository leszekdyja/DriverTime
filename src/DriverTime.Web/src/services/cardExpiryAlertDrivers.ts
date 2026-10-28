// The download API already verifies replacements within the authenticated company.
// If that request failed, retain expiry alerts rather than silently hiding them.
export function cardExpiryAlertDrivers<T extends { id: string }>(
    drivers: readonly T[], activeDownloads: readonly { driverId: string }[] | null,
): T[] {
    if (activeDownloads === null) return [...drivers];
    const activeIds = new Set(activeDownloads.map(download => download.driverId));
    return drivers.filter(driver => activeIds.has(driver.id));
}
