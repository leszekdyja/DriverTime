const ruleLabels: Record<string, string> = {
    "daily rest": "Odpoczynek dzienny",
    "weekly rest": "Odpoczynek tygodniowy",
    "reduced weekly rest": "Skrócony odpoczynek tygodniowy",
    "regular weekly rest": "Regularny odpoczynek tygodniowy",
    "daily driving limit": "Limit dziennego czasu jazdy",
    "weekly driving limit": "Limit tygodniowego czasu jazdy",
    "bi-weekly driving limit": "Limit jazdy w dwóch tygodniach",
    "continuous driving break": "Przerwa po 4h30 jazdy",
    "six 24-hour periods": "Sześć okresów 24-godzinnych",
    "weekly rest compensation": "Kompensacja odpoczynku tygodniowego",
    "reduced weekly rest compensation": "Kompensacja skróconego odpoczynku tygodniowego",
    "reduced daily rest counter": "Limit skróconych odpoczynków dziennych",
    "brak kraju rozpoczęcia": "Brak kraju rozpoczęcia",
    "brak kraju zakończenia": "Brak kraju zakończenia",
    "nieprawidłowy kod kraju": "Nieprawidłowy kod kraju",
    "niepełne dane kraju": "Niepełne dane kraju",
};

const codeLabels: Record<string, string> = {
    DAILY_REST: "Odpoczynek dzienny",
    REGULAR_WEEKLY_REST: "Regularny odpoczynek tygodniowy",
    REDUCED_WEEKLY_REST: "Skrócony odpoczynek tygodniowy",
    DAILY_DRIVING_LIMIT: "Limit dziennego czasu jazdy",
    WEEKLY_DRIVING_LIMIT: "Limit tygodniowego czasu jazdy",
    BI_WEEKLY_DRIVING_LIMIT: "Limit jazdy w dwóch tygodniach",
    CONTINUOUS_DRIVING_BREAK: "Przerwa po 4h30 jazdy",
    SIX_24H_PERIODS: "Sześć okresów 24-godzinnych",
    WEEKLY_REST_COMPENSATION: "Kompensacja odpoczynku tygodniowego",
    REDUCED_WEEKLY_REST_COMPENSATION: "Kompensacja skróconego odpoczynku tygodniowego",
    REDUCED_DAILY_REST_COUNTER: "Limit skróconych odpoczynków dziennych",
    MISSING_START_COUNTRY: "Brak kraju rozpoczęcia",
    MISSING_END_COUNTRY: "Brak kraju zakończenia",
    INVALID_COUNTRY_CODE: "Nieprawidłowy kod kraju",
    INCOMPLETE_COUNTRY_DATA: "Niepełne dane kraju",
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

export function getComplianceRuleLabel(ruleName?: string | null, code?: string | null) {
    const normalizedRule = ruleName?.trim().toLowerCase() ?? "";
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

    return category || "Brak kategorii";
}

export function getAlertStatusLabel(status?: string | null) {
    if (!status) return "Brak statusu";

    const normalized = status.trim().toLowerCase();

    if (normalized === "open") return "Otwarte";

    return status;
}
