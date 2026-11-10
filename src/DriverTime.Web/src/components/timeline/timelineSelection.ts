export type SelectionActivityKind = "DRIVING" | "REST" | "WORK" | "AVAILABILITY" | "UNKNOWN";

type SelectionSegment = {
    start: Date;
    end: Date;
    type: SelectionActivityKind;
    driverName: string | null;
    vehicleRegistration: string | null;
};

export function summarizeTimelineSelection(segments: SelectionSegment[], from: number, to: number) {
    const start = Math.min(from, to);
    const end = Math.max(from, to);
    const clipped = segments.filter((s) => s.start.getTime() < end && s.end.getTime() > start)
        .map((s) => ({ ...s, start: new Date(Math.max(start, s.start.getTime())), end: new Date(Math.min(end, s.end.getTime())) }));
    const boundaries = [...new Set([start, end, ...clipped.flatMap((s) => [s.start.getTime(), s.end.getTime()])])].sort((a, b) => a - b);
    const seconds: Record<SelectionActivityKind, number> = { DRIVING: 0, REST: 0, WORK: 0, AVAILABILITY: 0, UNKNOWN: 0 };
    let conflictingSeconds = 0;
    for (let i = 1; i < boundaries.length; i++) {
        const a = boundaries[i - 1];
        const b = boundaries[i];
        const kinds = new Set(clipped.filter((s) => s.start.getTime() < b && s.end.getTime() > a).map((s) => s.type));
        const duration = (b - a) / 1000;
        const kind = kinds.size === 1 ? [...kinds][0] : "UNKNOWN";
        seconds[kind] += duration;
        if (kinds.size > 1) conflictingSeconds += duration;
    }
    return {
        start, end, durationSeconds: (end - start) / 1000, seconds, conflictingSeconds,
        drivers: [...new Set(clipped.map((s) => s.driverName).filter(Boolean))],
        vehicles: [...new Set(clipped.map((s) => s.vehicleRegistration).filter((value): value is string => Boolean(value)))],
        segments: clipped.sort((a, b) => a.start.getTime() - b.start.getTime()),
    };
}
