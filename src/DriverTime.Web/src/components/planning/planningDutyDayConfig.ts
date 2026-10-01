export const planningDutyDayOptions = [
    [1, "Pon"],
    [2, "Wt"],
    [4, "Śr"],
    [8, "Czw"],
    [16, "Pt"],
    [32, "Sob"],
    [64, "Nd"],
] as const;

export const planningWeekdaysMask = 31;
export const planningWeekendMask = 96;
export const planningAllWeekMask = 127;

export function getPlanningDutyMask(activeDaysMask: number | null | undefined): number {
    return activeDaysMask ?? planningWeekdaysMask;
}

export function togglePlanningDutyDay(mask: number, dayBit: number, isEnabled: boolean): number {
    return isEnabled ? mask | dayBit : mask & ~dayBit;
}
