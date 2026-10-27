type RecordValue = Record<string, unknown>;
type Option = { id: string };
export type PreparedMappingScope = { operatingCompanyId: string | null; drivers: Option[]; duties: Option[] };

function record(value: unknown): RecordValue {
    if (!value || typeof value !== "object" || Array.isArray(value)) throw new Error("Nieprawidłowy plik dopasowań.");
    return value as RecordValue;
}

export function readPreparedLocalPlanningMappings(value: unknown, scope: PreparedMappingScope) {
    const snapshot = record(value);
    const profile = record(snapshot.oneTimeMappings);
    if (typeof snapshot.sourceId !== "string" || profile.sourceId !== snapshot.sourceId
        || typeof snapshot.exportedAtUtc !== "string" || profile.exportedAtUtc !== snapshot.exportedAtUtc)
        throw new Error("Dopasowania nie dotyczą tego eksportu. Nie można ich zastosować do innego pliku.");
    if (profile.operatingCompanyId !== scope.operatingCompanyId)
        throw new Error("Dopasowania przygotowano dla innego zakresu firmy. Wybierz właściwą firmę lub konto główne.");
    const tables = record(snapshot.tables);
    function mappings(key: string, table: unknown, options: Option[]) {
        if (!Array.isArray(table)) throw new Error("Brak tabeli źródłowej dopasowań.");
        const sourceIds = new Set(table.map(row => String(record(row).id)));
        const allowed = new Set(options.map(row => row.id));
        const result: Record<string, string> = {};
        for (const [sourceId, targetId] of Object.entries(record(profile[key]))) {
            if (!sourceIds.has(sourceId) || typeof targetId !== "string" || !allowed.has(targetId))
                throw new Error("Dopasowanie nie należy do bieżącej firmy lub źródła. Nie zastosowano zmian.");
            result[sourceId] = targetId;
        }
        return result;
    }
    const driverMappings = mappings("driverMappings", tables.drivers, scope.drivers);
    const dutyMappings = mappings("dutyMappings", tables.duties, scope.duties);
    if (new Set(Object.values(driverMappings)).size !== Object.keys(driverMappings).length)
        throw new Error("Dwóch kierowców wskazuje tę samą osobę. Nie zastosowano zmian.");
    return { driverMappings, dutyMappings };
}
