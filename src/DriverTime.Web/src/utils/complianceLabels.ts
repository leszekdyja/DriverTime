const ruleLabels: Record<string, string> = {
    "daily rest": "Odpoczynek dzienny",
    "weekly rest": "Odpoczynek tygodniowy",
    "reduced weekly rest": "Skrócony odpoczynek tygodniowy",
    "regular weekly rest": "Regularny odpoczynek tygodniowy",
    "daily driving": "Limit jazdy dziennej",
    "daily driving limit": "Limit jazdy dziennej",
    "weekly driving": "Limit jazdy tygodniowej",
    "weekly driving limit": "Limit jazdy tygodniowej",
    "biweekly driving": "Limit jazdy w dwóch kolejnych tygodniach",
    "bi-weekly driving": "Limit jazdy w dwóch kolejnych tygodniach",
    "bi-weekly driving limit": "Limit jazdy w dwóch kolejnych tygodniach",
    "continuous driving break": "Przerwa po 4 godz. 30 min jazdy",
    "six 24-hour periods": "Sześć okresów 24-godzinnych",
    "weekly rest compensation": "Rekompensata odpoczynku tygodniowego",
    "reduced weekly rest compensation": "Rekompensata skróconego odpoczynku tygodniowego",
    "reduced daily rest counter": "Limit skróconych odpoczynków dziennych",
    "brak kraju rozpoczęcia": "Brak kraju rozpoczęcia",
    "brak kraju zakończenia": "Brak kraju zakończenia",
    "nieprawidłowy kod kraju": "Nieprawidłowy kod kraju",
    "niepełne dane kraju": "Niekompletne dane kraju",
    "niekompletne dane kraju": "Niekompletne dane kraju",
};

const codeLabels: Record<string, string> = {
    DAILY_REST: "Odpoczynek dzienny",
    REGULAR_WEEKLY_REST: "Regularny odpoczynek tygodniowy",
    REDUCED_WEEKLY_REST: "Skrócony odpoczynek tygodniowy",
    DAILY_DRIVING: "Limit jazdy dziennej",
    DAILY_DRIVING_LIMIT: "Limit jazdy dziennej",
    WEEKLY_DRIVING: "Limit jazdy tygodniowej",
    WEEKLY_DRIVING_LIMIT: "Limit jazdy tygodniowej",
    BIWEEKLY_DRIVING: "Limit jazdy w dwóch kolejnych tygodniach",
    BIWEEKLY_DRIVING_LIMIT: "Limit jazdy w dwóch kolejnych tygodniach",
    BI_WEEKLY_DRIVING: "Limit jazdy w dwóch kolejnych tygodniach",
    BI_WEEKLY_DRIVING_LIMIT: "Limit jazdy w dwóch kolejnych tygodniach",
    CONTINUOUS_DRIVING_BREAK: "Przerwa po 4 godz. 30 min jazdy",
    SIX_24H_PERIODS: "Sześć okresów 24-godzinnych",
    WEEKLY_REST_COMPENSATION: "Rekompensata odpoczynku tygodniowego",
    REDUCED_WEEKLY_REST_COMPENSATION: "Rekompensata skróconego odpoczynku tygodniowego",
    REDUCED_DAILY_REST_COUNTER: "Limit skróconych odpoczynków dziennych",
    MISSING_START_COUNTRY: "Brak kraju rozpoczęcia",
    MISSING_END_COUNTRY: "Brak kraju zakończenia",
    INVALID_COUNTRY_CODE: "Nieprawidłowy kod kraju",
    INCOMPLETE_COUNTRY_DATA: "Niekompletne dane kraju",
};

const severityLabels: Record<string, string> = {
    critical: "Krytyczne",
    high: "Krytyczne",
    severe: "Krytyczne",
    "very serious": "Krytyczne",
    "very-serious": "Krytyczne",
    warning: "Ostrzeżenie",
    medium: "Ostrzeżenie",
    serious: "Ostrzeżenie",
    low: "Niskie",
    info: "Informacyjne",
};

const statusLabels: Record<string, string> = {
    open: "Otwarte",
    completed: "Zakończono",
    failed: "Niepowodzenie",
    pending: "Oczekuje",
    processing: "W trakcie",
    running: "W trakcie",
    queued: "W kolejce",
    retrying: "Ponawianie",
    warning: "Ostrzeżenie",
    overdue: "Po terminie",
    nodata: "Brak danych",
    "no data": "Brak danych",
    notachographdata: "Brak danych z tachografu",
    "no tachograph data": "Brak danych z tachografu",
};

export function getComplianceRuleLabel(ruleName?: string | null, code?: string | null) {
    const normalizedRule = ruleName?.trim().toLowerCase().replaceAll("_", " ") ?? "";
    const normalizedCode = code?.trim().toUpperCase() ?? "";

    return ruleLabels[normalizedRule] || codeLabels[normalizedCode] || "Inne naruszenie";
}

export function getSeverityLabel(severity?: string | null) {
    const normalized = severity?.trim().toLowerCase().replaceAll("_", " ") ?? "";

    return severityLabels[normalized] || "Informacyjne";
}

export function getAlertCategoryLabel(category?: string | null) {
    if (category === "Downloads") return "Odczyty";
    if (category === "Imports") return "Importy";
    if (category === "Compliance") return "Zgodność";

    return category ? "Inna kategoria" : "Brak kategorii";
}

export function getAlertStatusLabel(status?: string | null) {
    if (!status) return "Brak statusu";

    const normalized = status.trim().toLowerCase().replaceAll("_", " ");

    return statusLabels[normalized] || status;
}
