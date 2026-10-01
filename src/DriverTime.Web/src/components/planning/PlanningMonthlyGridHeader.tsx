import type { PlanningMonthDay } from "./buildPlanningMonthlyGrid";

type PlanningMonthlyGridHeaderProps = {
    days: PlanningMonthDay[];
};

export function PlanningMonthlyGridHeader({ days }: PlanningMonthlyGridHeaderProps) {
    return (
        <thead>
            <tr>
                <th className="planning-grid-driver-heading">Kierowca</th>
                {days.map((day) => (
                    <th
                        key={day.date}
                        className={`planning-grid-day-heading${day.isSaturday ? " saturday" : ""}${day.isSunday || day.isHoliday ? " holiday" : ""}`}
                    >
                        <span>{day.day}</span>
                        <small>{day.weekday}</small>
                    </th>
                ))}
            </tr>
        </thead>
    );
}
