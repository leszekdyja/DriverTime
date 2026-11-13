type Activity = {
    startUtc: string;
    endUtc: string;
    activityType: string;
    isCrew?: boolean | null;
    cardSlot?: number | null;
};

// Display counter only; the backend remains the source of compliance decisions.
export function drivingSecondsSinceBreak(activities: Activity[], endTime: number): number {
    const ordered = activities.map(a => ({ ...a, start: Date.parse(a.startUtc), end: Math.min(Date.parse(a.endUtc), endTime) }))
        .filter(a => Number.isFinite(a.start) && Number.isFinite(a.end) && a.end > a.start)
        .sort((a, b) => a.start - b.start || a.end - b.end);
    const segments: Array<{ start: number; end: number; type: string; crewBreak: boolean }> = [];
    for (const activity of ordered) {
        const type = activity.activityType.trim().toUpperCase();
        const crewBreak = type === "AVAILABILITY" && activity.isCrew === true && activity.cardSlot === 2;
        const previous = segments.at(-1);
        if (previous && previous.end === activity.start && previous.type === type
            && (type === "REST" || (crewBreak && previous.crewBreak))) {
            previous.end = activity.end;
        } else segments.push({ start: activity.start, end: activity.end, type, crewBreak });
    }
    let counter = 0;
    let firstSplit = false;
    let coveredUntil = -Infinity;
    for (const segment of segments) {
        const start = Math.max(segment.start, coveredUntil);
        const seconds = Math.max(0, (segment.end - start) / 1000);
        coveredUntil = Math.max(coveredUntil, segment.end);
        if ((segment.type === "REST" || segment.crewBreak) && seconds >= 45 * 60) {
            counter = 0;
            firstSplit = false;
        } else if (segment.type === "REST" && firstSplit && seconds >= 30 * 60) {
            counter = 0;
            firstSplit = false;
        } else if (segment.type === "REST" && seconds >= 15 * 60) {
            firstSplit = true;
        } else if (segment.type === "DRIVING") counter += seconds;
    }
    return counter;
}
