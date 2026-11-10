import type { DriverViolation } from "../services/violationsService";

function duration(minutes: number) {
    const hours = Math.floor(minutes / 60);
    const rest = minutes % 60;
    return hours ? `${hours} godz.${rest ? ` ${rest} min` : ""}` : `${rest} min`;
}

function stamp(value?: string | null) {
    if (!value || !Number.isFinite(Date.parse(value))) return null;
    return new Intl.DateTimeFormat("pl-PL", { dateStyle: "short", timeStyle: "short", timeZone: "Europe/Warsaw" }).format(new Date(value));
}

export function drivingBreakExplanation(violation: DriverViolation) {
    const analysis = violation.ruleAnalysis;
    if (![violation.code, analysis?.ruleCode].some((code) => code?.toUpperCase().includes("CONTINUOUS_DRIVING"))) return null;
    const details = violation.businessDetails;
    const driving = analysis?.continuousDrivingMinutes ?? details?.continuousDrivingMinutes ?? violation.actualValueMinutes;
    const limit = analysis?.drivingLimitMinutes ?? details?.drivingLimitMinutes;
    const excess = analysis?.exceededMinutes ?? details?.drivingExceededMinutes ?? violation.excessMinutes;
    const lines: string[] = [];
    if (driving != null && limit != null) {
        lines.push(`W zapisanej analizie zsumowano ${duration(driving)} jazdy. Limit w tej analizie wynosi ${duration(limit)}.`);
    } else {
        lines.push("Zapis naruszenia nie zawiera pełnego porównania czasu jazdy z limitem. Nie można z niego odtworzyć całego obliczenia.");
    }
    if (excess != null && excess > 0) lines.push(`Zapisane przekroczenie: ${duration(excess)}. To czas jazdy ponad limit, a nie długość brakującej przerwy.`);
    if (analysis?.isEstimated) lines.push("Analiza jest szacunkowa — wymaga sprawdzenia z danymi źródłowymi.");
    const detected = stamp(analysis?.violationDetectedAtUtc ?? violation.occurredAtUtc);
    if (detected) lines.push(`Moment wskazany w zapisie naruszenia: ${detected} (czas polski).`);
    lines.push("Jazda jest sumowana także wtedy, gdy pomiędzy jej odcinkami była inna praca. Sama inna praca nie zeruje licznika jazdy.");
    lines.push("Dla standardowej reguły przerwy: 45 minut jednorazowo albo co najmniej 15 minut, a następnie co najmniej 30 minut. Pierwsza część sama nie zeruje licznika jazdy.");
    const breaks = (analysis?.segments ?? []).filter((segment) => segment.activityType.toUpperCase() === "REST")
        .map((segment) => {
            const from = stamp(segment.startUtc);
            const to = stamp(segment.endUtc);
            return `${from ?? "Brak godziny"} – ${to ?? "Brak godziny"}: ${duration(segment.durationMinutes)} odpoczynku. ${segment.resetsCounter ? "W zapisanej analizie zeruje licznik jazdy." : "W tym miejscu zapisana analiza nie zeruje licznika jazdy."}`;
        });
    return { lines, breaks, note: "To objaśnienie zapisanego wyniku, nie ponowne przeliczenie dnia ani ocena, czy kierowca może teraz jechać. Jeżeli wynik nie zgadza się z wykresem, sprawdź pełną analizę reguły i aktualność przeliczenia." };
}
