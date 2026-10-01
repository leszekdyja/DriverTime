type PlanningMonthNavigationProps = {
    year: number;
    month: number;
    onPreviousMonth: () => void;
    onNextMonth: () => void;
    onCurrentMonth: () => void;
    onMonthChange: (year: number, month: number) => void;
};

const monthNames = [
    "Styczeń",
    "Luty",
    "Marzec",
    "Kwiecień",
    "Maj",
    "Czerwiec",
    "Lipiec",
    "Sierpień",
    "Wrzesień",
    "Październik",
    "Listopad",
    "Grudzień",
];

export function PlanningMonthNavigation({
    year,
    month,
    onPreviousMonth,
    onNextMonth,
    onCurrentMonth,
    onMonthChange,
}: PlanningMonthNavigationProps) {
    return (
        <div className="planning-month-navigation">
            <button className="planning-secondary-button" type="button" onClick={onPreviousMonth}>Poprzedni</button>
            <label>Miesiąc
                <select value={month} onChange={(event) => onMonthChange(year, Number(event.target.value))}>
                    {monthNames.map((name, index) => <option key={name} value={index + 1}>{name}</option>)}
                </select>
            </label>
            <label>Rok
                <input type="number" min="2000" max="2100" value={year} onChange={(event) => onMonthChange(Number(event.target.value), month)} />
            </label>
            <button className="planning-secondary-button" type="button" onClick={onCurrentMonth}>Bieżący miesiąc</button>
            <button className="planning-secondary-button" type="button" onClick={onNextMonth}>Następny</button>
        </div>
    );
}
